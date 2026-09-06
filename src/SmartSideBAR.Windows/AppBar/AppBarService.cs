// Windows/AppBar/AppBarService.cs —— §5.3 生命周期 + 通知状态机 (v1.2.0 缺口 K1/K2/K3/K4/K7 根修)
// 通知链: 系统 → WndProcHook(子类化) → OnWndProc 状态机 → Reposition/EventBus。
// 坐标 (K4): 与系统协商及窗口定位全链路物理像素 —— MoveWindow 直达, 无 DIP 换算。
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Windows.AppBar;

// ---- EventBus 事件 (UI 层订阅以同步面板布局) ----
public sealed record AppBarGeometryChanged(AppBarRect Rect, int WidthPx);
public sealed record FullscreenAppChanged(bool Active);
public sealed record WindowArrangePending(bool Hiding);

/// <summary>侧栏应用栏编排器。
/// 生命周期: Attach(自愈+注册+首次定位) ⇄ OnAppBarNotification(自适应) → Detach(注销)。</summary>
public sealed class AppBarService(
    IAppBarApi api,
    IWndProcHook hook,
    IEventBus bus,
    ILogger<AppBarService>? log = null) : IDisposable
{
    /// <summary>v1.1 悬浮玻璃胶囊: 上下各留 8% 工作区高度 (不顶到屏幕上下缘)。</summary>
    public const double CapsuleMarginRatio = 0.08;

    private nint _hwnd;
    private uint _edge = AppBarNative.ABE_RIGHT;
    private int _railPx = 64;      // rail 收起宽度 (物理像素)
    private int _expandedPx = 420; // 面板展开宽度 (物理像素)
    private bool _registered;
    private bool _fullscreen;      // K3: 全屏应用在场标志
    private bool _userExpanded;    // 用户偏好; 全屏结束后据此恢复 (V5)
    private bool _docked;          // v1.1 dock 语义: 收起为角落小方块

    public bool IsRegistered => _registered;
    public AppBarRect? GrantedRect { get; private set; }
    public bool FullscreenActive => _fullscreen;
    public int CurrentWidthPx => _userExpanded ? _expandedPx : _railPx;
    public bool Docked => _docked;

    /// <summary>注册 + 首次定位。失败返回 false (调用方回退 Topmost 置顶, 与 v1.2.0 同策略)。</summary>
    public bool Attach(nint hwnd, AppBarEdge edge, int railWidthPx, int expandedWidthPx)
    {
        if (_registered) Detach();
        _hwnd = hwnd;
        _edge = Map(edge);
        _railPx = Math.Max(2, railWidthPx);
        _expandedPx = Math.Max(_railPx, expandedWidthPx);
        _userExpanded = false;
        _fullscreen = false;

        // V6 自愈: 上次强杀可能残留孤儿注册 —— ABM_REMOVE 对未注册 hwnd 无副作用, 先补发一次
        api.Remove(hwnd);

        if (!api.Register(hwnd, AppBarNative.AppBarCallbackMsg))
        {
            log?.LogWarning("[AppBar] 注册失败 (可能与他栏冲突), 回退 alwaysOnTop 模式");
            return false;
        }
        _registered = true;

        // K1/K2/K3 核心: 挂接 WndProc —— v1.2.0 完全缺失的通知闭环
        hook.Add(hwnd, OnWndProc);
        Reposition(CurrentWidthPx);
        log?.LogInformation("[AppBar] 已注册 edge={Edge} rail={Rail}px expanded={Expanded}px",
            edge, _railPx, _expandedPx);
        return true;
    }

    /// <summary>展开/收起 (window:resize 通道落点)。</summary>
    public void SetExpanded(bool expanded)
    {
        _userExpanded = expanded;
        Reposition(CurrentWidthPx);
    }

    /// <summary>v1.1 dock 语义: docked = 指定边距尺寸的小方块停靠在工作区底角。</summary>
    public void SetDocked(bool docked, int dockSizePx)
    {
        _docked = docked;
        _dockSizePx = Math.Max(2, dockSizePx);
        Reposition(CurrentWidthPx);
    }
    private int _dockSizePx = 78;

    /// <summary>重排: 注册后 / 展开收起 / ABN_POSCHANGED / 分辨率与任务栏变化 (K1/K4)。
    /// 锚点必须是 rcMonitor 全屏矩形: rcWork 已扣除自身 AppBar 占位, 若以它为锚,
    /// 每次 Reposition 都会向屏幕内侧漂移一个带宽 —— 对方案 §5.3 蓝本的勘误。
    /// 垂直范围 = 工作区高度 84% 的悬浮胶囊 (上下各留 8%, 对齐 v1.1 玻璃胶囊设计);
    /// 水平锚定显示器边缘, 沿边裁剪交给 ABM_QUERYPOS。</summary>
    public void Reposition(int widthPx)
    {
        if (!_registered || _hwnd == 0) return;
        var mon = api.GetMonitorOf(_hwnd);

        // v1.1 dock 语义: 工作区底角小方块 (side-aware)
        if (_docked && (_edge is AppBarNative.ABE_LEFT or AppBarNative.ABE_RIGHT))
        {
            // 先注销自身旧占位: QUERYPOS 会为已注册 AppBar(含我们自己)让位, 否则方块逐次左漂。
            // ABM_REMOVE 后系统 WorkArea 恢复滞后一步 —— 收敛循环直至授予位==期望位。
            var dockWant = _edge == AppBarNative.ABE_RIGHT
                ? new AppBarRect(mon.Work.X + mon.Work.W - _dockSizePx,
                    mon.Work.Y + mon.Work.H - _dockSizePx, _dockSizePx, _dockSizePx)
                : new AppBarRect(mon.Work.X, mon.Work.Y + mon.Work.H - _dockSizePx, _dockSizePx, _dockSizePx);
            var dockGranted = dockWant;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                api.Remove(_hwnd);
                dockGranted = api.QueryAndSetPos(_hwnd, _edge, dockWant);
                if (Math.Abs(dockGranted.X - dockWant.X) <= 2 && Math.Abs(dockGranted.Y - dockWant.Y) <= 2)
                {
                    break;
                }
            }
            api.WindowPosChanged(_hwnd);
            if (!api.MoveWindow(_hwnd, dockGranted))
            {
                log?.LogWarning("[AppBar] MoveWindow 失败 (hwnd=0x{Hwnd:X})", _hwnd);
            }
            GrantedRect = dockGranted;
            bus.Publish(new AppBarGeometryChanged(dockGranted, _dockSizePx));
            log?.LogInformation("[AppBar] dock 重排 → ({X},{Y}) {W}x{H}px", dockGranted.X, dockGranted.Y, dockGranted.W, dockGranted.H);
            return;
        }

        var capsuleH = (int)Math.Round(mon.Work.H * (1 - 2 * CapsuleMarginRatio));
        var capsuleY = mon.Work.Y + (int)Math.Round(mon.Work.H * CapsuleMarginRatio);
        var want = _edge switch
        {
            AppBarNative.ABE_LEFT => new AppBarRect(mon.Monitor.X, capsuleY, widthPx, capsuleH),
            AppBarNative.ABE_RIGHT => new AppBarRect(mon.Monitor.X + mon.Monitor.W - widthPx, capsuleY, widthPx, capsuleH),
            AppBarNative.ABE_TOP => new AppBarRect(mon.Monitor.X, mon.Monitor.Y, mon.Monitor.W, widthPx),
            _ => new AppBarRect(mon.Monitor.X, mon.Monitor.Y + mon.Monitor.H - widthPx, mon.Monitor.W, widthPx),
        };
        var granted = api.QueryAndSetPos(_hwnd, _edge, want); // 系统审批 (避让任务栏/他栏)
        api.WindowPosChanged(_hwnd);                          // K2: 位置变更上报
        if (!api.MoveWindow(_hwnd, granted))                  // K4: 物理像素直达, 无 DIP 换算
        {
            log?.LogWarning("[AppBar] MoveWindow 失败 (hwnd=0x{Hwnd:X})", _hwnd);
        }
        GrantedRect = granted;
        bus.Publish(new AppBarGeometryChanged(granted, widthPx));
        log?.LogInformation("[AppBar] 重排 → ({X},{Y}) {W}x{H}px", granted.X, granted.Y, granted.W, granted.H);
    }

    /// <summary>WndProc 状态机 —— K1/K2/K3/K7 的通知闭环。</summary>
    private nint OnWndProc(nint hwnd, uint msg, nuint wParam, nint lParam, ref bool handled)
    {
        if (msg == AppBarNative.AppBarCallbackMsg)
        {
            handled = true;
            switch (wParam)
            {
                case AppBarNative.ABN_POSCHANGED: // 任务栏换边/他栏变化/WorkArea 变
                    Reposition(CurrentWidthPx);
                    break;

                case AppBarNative.ABN_FULLSCREENAPP: // 授课视频等全屏应用开/关 (K3)
                    _fullscreen = lParam != 0;
                    bus.Publish(new FullscreenAppChanged(_fullscreen));
                    // 策略: 全屏在场保持注册 (教师仍可唤出) 但先收起为 rail; 关闭后按用户偏好恢复 (V5)
                    Reposition(_fullscreen ? _railPx : CurrentWidthPx);
                    break;

                case AppBarNative.ABN_STATECHANGE: // 任务栏「自动隐藏/总在顶部」切换
                    Reposition(CurrentWidthPx);
                    break;

                case AppBarNative.ABN_WINDOWARRANGE: // 层叠/平铺: TRUE=即将排列 FALSE=完成
                    bus.Publish(new WindowArrangePending(wParam != 0));
                    break;
            }
            return 0;
        }

        if (!_registered) return 0;
        if (msg == AppBarNative.WM_ACTIVATE)
        {
            api.Activate(_hwnd); // K2: 激活状态上报 (多 AppBar z 序维护)
        }
        else if (msg is AppBarNative.WM_DISPLAYCHANGE or AppBarNative.WM_DPICHANGED)
        {
            Reposition(CurrentWidthPx); // K7: 分辨率/DPI 变化重协商 (ABN_POSCHANGED 通常已带一次, 幂等)
        }
        return 0; // handled=false → 其余消息交还原窗口过程
    }

    /// <summary>退出/窗口销毁前必须调用 —— 否则系统 WorkArea 泄漏 (v1.2.0 语义保留)。</summary>
    public void Detach()
    {
        if (!_registered) return;
        hook.Remove(_hwnd, OnWndProc);
        api.Remove(_hwnd);
        _registered = false;
        log?.LogInformation("[AppBar] 已注销, 系统 WorkArea 恢复");
    }

    public void Dispose() => Detach();

    private static uint Map(AppBarEdge edge) => edge switch
    {
        AppBarEdge.Left => AppBarNative.ABE_LEFT,
        AppBarEdge.Top => AppBarNative.ABE_TOP,
        AppBarEdge.Right => AppBarNative.ABE_RIGHT,
        _ => AppBarNative.ABE_BOTTOM,
    };
}
