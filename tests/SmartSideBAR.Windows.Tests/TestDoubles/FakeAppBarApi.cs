// Windows.Tests 假件 —— 记录 SHAppBarMessage 调用序列, 供状态机断言 (方案 §7.1)
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Windows.Tests.TestDoubles;

/// <summary>假 AppBar API: 记录操作序列, 模拟系统授予矩形。</summary>
public sealed class FakeAppBarApi : IAppBarApi
{
    /// <summary>全部操作的时间线: NEW / REMOVE / QUERYPOS / SETPOS / ACTIVATE / WINDOWPOSCHANGED / MOVE</summary>
    public List<string> Ops { get; } = [];

    public List<(nint Hwnd, uint CallbackMsg)> RegisterCalls { get; } = [];
    public List<nint> RemoveCalls { get; } = [];
    public List<(nint Hwnd, uint Edge, AppBarRect Want)> QueryCalls { get; } = [];
    public List<nint> ActivateCalls { get; } = [];
    public List<(nint Hwnd, AppBarRect Rect)> MoveRequests { get; } = [];
    public int WindowPosChangedCalls { get; private set; }

    public bool RegisterResult { get; set; } = true;

    /// <summary>模拟的显示器几何: 1920×1080, 底部 40px 任务栏 → 工作区 1920×1040</summary>
    public AppBarRect Monitor { get; set; } = new(0, 0, 1920, 1080);
    public AppBarRect Work { get; set; } = new(0, 0, 1920, 1040);

    public bool Register(nint hwnd, uint callbackMsg)
    {
        Ops.Add("NEW");
        RegisterCalls.Add((hwnd, callbackMsg));
        return RegisterResult;
    }

    public void Remove(nint hwnd)
    {
        Ops.Add("REMOVE");
        RemoveCalls.Add(hwnd);
    }

    public AppBarRect QueryAndSetPos(nint hwnd, uint edge, AppBarRect want)
    {
        Ops.Add("QUERYPOS");
        QueryCalls.Add((hwnd, edge, want));
        Ops.Add("SETPOS");
        // 系统行为模拟: 沿边方向裁剪到工作区 (任务栏避让), 垂直方向保留提议带宽
        return edge switch
        {
            AppBarNative.ABE_RIGHT => new AppBarRect(Monitor.W - want.W, Work.Y, want.W, Work.H),
            AppBarNative.ABE_LEFT => new AppBarRect(Work.X, Work.Y, want.W, Work.H),
            AppBarNative.ABE_TOP => new AppBarRect(Work.X, Work.Y, Monitor.W, want.H),
            _ => new AppBarRect(Work.X, Work.Y + Work.H - want.H, Monitor.W, want.H),
        };
    }

    public void Activate(nint hwnd) { Ops.Add("ACTIVATE"); ActivateCalls.Add(hwnd); }

    public void WindowPosChanged(nint hwnd) { Ops.Add("WINDOWPOSCHANGED"); WindowPosChangedCalls++; }

    public bool MoveWindow(nint hwnd, AppBarRect rect)
    {
        Ops.Add("MOVE");
        MoveRequests.Add((hwnd, rect));
        return true;
    }

    public TaskbarInfo? GetTaskbarPos() => new(AppBarNative.ABE_BOTTOM, new AppBarRect(0, 1040, 1920, 40));

    public MonitorRects GetMonitorOf(nint hwnd) => new(Monitor, Work);
}

/// <summary>假 WndProc 钩子: 捕获回调, 测试可直接注入系统消息。</summary>
public sealed class FakeWndProcHook : IWndProcHook
{
    private readonly Dictionary<nint, List<WndProcCallback>> _callbacks = [];

    public bool HasHook(nint hwnd) => _callbacks.ContainsKey(hwnd);

    public void Add(nint hwnd, WndProcCallback callback)
    {
        if (!_callbacks.TryGetValue(hwnd, out var list))
        {
            list = [];
            _callbacks[hwnd] = list;
        }
        list.Add(callback);
    }

    public void Remove(nint hwnd, WndProcCallback callback)
    {
        if (_callbacks.TryGetValue(hwnd, out var list))
        {
            list.Remove(callback);
            if (list.Count == 0) _callbacks.Remove(hwnd);
        }
    }

    /// <summary>向 hwnd 投递一条消息 (模拟系统 ABN_* / WM_* 通知)。</summary>
    public (nint Result, bool Handled) Simulate(nint hwnd, uint msg, nuint wParam, nint lParam)
    {
        if (_callbacks.TryGetValue(hwnd, out var list))
        {
            foreach (var cb in list.ToArray())
            {
                var handled = false;
                var result = cb(hwnd, msg, wParam, lParam, ref handled);
                if (handled) return (result, true);
            }
        }
        return (0, false);
    }
}

public static class TestEnv
{
    public static (AppBarService Svc, FakeAppBarApi Api, FakeWndProcHook Hook, EventBus Bus,
        List<AppBarGeometryChanged> Geometry, List<FullscreenAppChanged> Fullscreen,
        List<WindowArrangePending> Arrange) CreateService()
    {
        var api = new FakeAppBarApi();
        var hook = new FakeWndProcHook();
        var bus = new EventBus();
        var geometry = new List<AppBarGeometryChanged>();
        var fullscreen = new List<FullscreenAppChanged>();
        var arrange = new List<WindowArrangePending>();
        bus.Subscribe<AppBarGeometryChanged>(geometry.Add);
        bus.Subscribe<FullscreenAppChanged>(fullscreen.Add);
        bus.Subscribe<WindowArrangePending>(arrange.Add);
        var svc = new AppBarService(api, hook, bus);
        return (svc, api, hook, bus, geometry, fullscreen, arrange);
    }
}
