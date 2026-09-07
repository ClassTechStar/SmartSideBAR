// Windows/AppBar/AppBarApi.cs —— SHAppBarMessage 真实现; QUERYPOS 语义逐行对齐 src/native/appbar.cc
// 所有调用经 PhysicalDpiScope 强制 PMv2 线程上下文 —— 宿主线程上下文不确定, 物理像素必须自证 (K4)。
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Windows.AppBar;

public sealed class AppBarApi(ILogger<AppBarApi>? log = null) : IAppBarApi
{
    /// <summary>PMv2 线程上下文作用域: 块内所有 Win32 坐标为物理像素。</summary>
    public readonly struct PhysicalDpiScope : IDisposable
    {
        private readonly nint _prev;
        public PhysicalDpiScope()
        {
            _prev = AppBarNative.SetThreadDpiAwarenessContext(
                AppBarNative.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        }
        public void Dispose()
        {
            if (_prev != 0) AppBarNative.SetThreadDpiAwarenessContext(_prev);
        }
    }

    public bool Register(nint hwnd, uint callbackMsg)
    {
        if (hwnd == 0) return false;
        using var scope = new PhysicalDpiScope();
        var abd = new AppBarNative.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<AppBarNative.APPBARDATA>(),
            hWnd = hwnd,
            uCallbackMessage = callbackMsg,
        };
        var ok = AppBarNative.SHAppBarMessage(AppBarNative.ABM_NEW, ref abd) != 0;
        log?.LogInformation("[AppBar] ABM_NEW hwnd=0x{Hwnd:X} → {Ok}", hwnd, ok);
        return ok;
    }

    public void Remove(nint hwnd)
    {
        if (hwnd == 0) return;
        using var scope = new PhysicalDpiScope();
        var abd = new AppBarNative.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<AppBarNative.APPBARDATA>(),
            hWnd = hwnd,
        };
        _ = AppBarNative.SHAppBarMessage(AppBarNative.ABM_REMOVE, ref abd);
        log?.LogInformation("[AppBar] ABM_REMOVE hwnd=0x{Hwnd:X}", hwnd);
    }

    public AppBarRect QueryAndSetPos(nint hwnd, uint edge, AppBarRect want)
    {
        using var scope = new PhysicalDpiScope();
        var abd = new AppBarNative.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<AppBarNative.APPBARDATA>(),
            hWnd = hwnd,
            uEdge = edge,
            rc = new AppBarNative.RECT
            {
                Left = want.X,
                Top = want.Y,
                Right = want.X + want.W,
                Bottom = want.Y + want.H,
            },
        };

        // Query: 系统按任务栏/其他 AppBar 调整区间 (只动区间, 不保尺寸)
        _ = AppBarNative.SHAppBarMessage(AppBarNative.ABM_QUERYPOS, ref abd);

        // 与 appbar.cc 一致: 按提议边恢复尺寸 (沿边维度保留系统裁剪 —— 自动避开任务栏)
        switch (abd.uEdge)
        {
            case AppBarNative.ABE_LEFT: abd.rc.Right = abd.rc.Left + want.W; break;
            case AppBarNative.ABE_RIGHT: abd.rc.Left = abd.rc.Right - want.W; break;
            case AppBarNative.ABE_TOP: abd.rc.Bottom = abd.rc.Top + want.H; break;
            case AppBarNative.ABE_BOTTOM: abd.rc.Top = abd.rc.Bottom - want.H; break;
        }

        // Set: 系统更新内部 WorkArea
        _ = AppBarNative.SHAppBarMessage(AppBarNative.ABM_SETPOS, ref abd);

        return new AppBarRect(abd.rc.Left, abd.rc.Top, abd.rc.W, abd.rc.H);
    }

    public void Activate(nint hwnd)
    {
        using var scope = new PhysicalDpiScope();
        var abd = new AppBarNative.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<AppBarNative.APPBARDATA>(),
            hWnd = hwnd,
        };
        _ = AppBarNative.SHAppBarMessage(AppBarNative.ABM_ACTIVATE, ref abd);
    }

    public void WindowPosChanged(nint hwnd)
    {
        using var scope = new PhysicalDpiScope();
        var abd = new AppBarNative.APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<AppBarNative.APPBARDATA>(),
            hWnd = hwnd,
        };
        _ = AppBarNative.SHAppBarMessage(AppBarNative.ABM_WINDOWPOSCHANGED, ref abd);
    }

    public bool MoveWindow(nint hwnd, AppBarRect rect)
    {
        using var scope = new PhysicalDpiScope();
        return AppBarNative.MoveWindow(hwnd, rect.X, rect.Y, rect.W, rect.H, true);
    }

    public TaskbarInfo? GetTaskbarPos()
    {
        using var scope = new PhysicalDpiScope();
        var abd = new AppBarNative.APPBARDATA { cbSize = (uint)Marshal.SizeOf<AppBarNative.APPBARDATA>() };
        _ = AppBarNative.SHAppBarMessage(AppBarNative.ABM_GETTASKBARPOS, ref abd);
        if (abd.rc.W <= 0 || abd.rc.H <= 0) return null;
        var rect = new AppBarRect(abd.rc.Left, abd.rc.Top, abd.rc.W, abd.rc.H);
        return new TaskbarInfo(InferEdge(rect), rect); // 系统不填 uEdge, 由矩形对显示器推断
    }

    public MonitorRects GetMonitorOf(nint hwnd)
    {
        using var scope = new PhysicalDpiScope();
        var hMonitor = AppBarNative.MonitorFromWindow(hwnd, AppBarNative.MONITOR_DEFAULTTONEAR);
        var mi = new AppBarNative.MONITORINFO { cbSize = (uint)Marshal.SizeOf<AppBarNative.MONITORINFO>() };
        if (!AppBarNative.GetMonitorInfo(hMonitor, ref mi))
        {
            // 兜底: 主显示器 96DPI 假设 (仅极端失败路径)
            log?.LogWarning("[AppBar] GetMonitorInfo 失败, 使用主屏兜底矩形");
            return new MonitorRects(new AppBarRect(0, 0, 1920, 1080), new AppBarRect(0, 0, 1920, 1040));
        }
        return new MonitorRects(
            new AppBarRect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.W, mi.rcMonitor.H),
            new AppBarRect(mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.W, mi.rcWork.H));
    }

    private static uint InferEdge(AppBarRect rect)
    {
        var hMonitor = AppBarNative.MonitorFromPoint(new AppBarNative.POINT(rect.X, rect.Y), 1 /*MONITOR_DEFAULTTOPRIMARY*/);
        var mi = new AppBarNative.MONITORINFO { cbSize = (uint)Marshal.SizeOf<AppBarNative.MONITORINFO>() };
        if (!AppBarNative.GetMonitorInfo(hMonitor, ref mi)) return AppBarNative.ABE_BOTTOM;
        if (rect.Y == mi.rcMonitor.Top && rect.H != mi.rcMonitor.H) return AppBarNative.ABE_TOP;
        if (rect.Bottom == mi.rcMonitor.Bottom && rect.H != mi.rcMonitor.H) return AppBarNative.ABE_BOTTOM;
        if (rect.X == mi.rcMonitor.Left) return AppBarNative.ABE_LEFT;
        return AppBarNative.ABE_RIGHT;
    }
}
