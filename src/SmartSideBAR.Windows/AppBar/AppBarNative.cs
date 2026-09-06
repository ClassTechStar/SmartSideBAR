// Windows/AppBar/AppBarNative.cs —— §5.2 P/Invoke 层 (方案附录蓝本落地)
// 物理(K4): APPBARDATA/RECT/MONITORINFO 全部物理像素 —— 与系统协商的唯一坐标轨。
// 参考: https://learn.microsoft.com/zh-cn/windows/win32/shell/application-desktop-toolbars
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SmartSideBAR.Windows.AppBar;

internal static partial class AppBarNative
{
    // ---- ABM_* 消息 (SHAppBarMessage 第一参数) ----
    public const uint ABM_NEW = 0x0000;
    public const uint ABM_REMOVE = 0x0001;
    public const uint ABM_QUERYPOS = 0x0002;
    public const uint ABM_SETPOS = 0x0003;
    public const uint ABM_GETTASKBARPOS = 0x0005;
    public const uint ABM_ACTIVATE = 0x0006;           // K2: WM_ACTIVATE 时上报
    public const uint ABM_GETAUTOHIDEBAR = 0x0007;
    public const uint ABM_SETAUTOHIDEBAR = 0x0008;     // K6: 自动隐藏 (P2)
    public const uint ABM_WINDOWPOSCHANGED = 0x0009;   // K2: WM_WINDOWPOSCHANGED 时上报

    // ---- ABN_* 通知码 (回调消息 wParam) ----
    public const uint ABN_STATECHANGE = 0x0000;
    public const uint ABN_POSCHANGED = 0x0001;         // K1
    public const uint ABN_FULLSCREENAPP = 0x0002;      // K3
    public const uint ABN_WINDOWARRANGE = 0x0003;

    // ---- 边缘 ----
    public const uint ABE_LEFT = 0, ABE_TOP = 1, ABE_RIGHT = 2, ABE_BOTTOM = 3;

    // ---- 消息 ----
    public const uint WM_ACTIVATE = 0x0006;
    public const uint WM_DISPLAYCHANGE = 0x007E;       // K7: 显示器变化
    public const uint WM_DPICHANGED = 0x02E0;
    public const uint WM_DEVICECHANGE = 0x0219;        // Wave B UsbWatcher 复用

    /// <summary>应用栏回调消息: WM_APP + 0xBAB (应用自定义区; 刻意避开 v1.2.0 的 WM_USER+1,
    /// 防止与任何遗留注册/其他子类链冲突, 同时便于孤儿注册自愈识别)。</summary>
    public const uint AppBarCallbackMsg = 0x8000 + 0x0BAB;

    public const uint MONITOR_DEFAULTTONEAR = 2;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public POINT(int x, int y) { X = x; Y = y; }
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left, Top, Right, Bottom;

        public readonly int W => Right - Left;
        public readonly int H => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public uint cbSize;
        public nint hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public nint lParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [LibraryImport("shell32.dll")]
    internal static partial nuint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    public static readonly nint DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;

    /// <summary>切换调用线程 DPI 感知上下文, 返回原上下文 (恢复用)。
    /// 宿主 (Avalonia) 线程上下文不受我们控制 —— AppBar 协商一律临时切到 PMv2,
    /// 保证 MonitorInfo/MoveWindow 全部物理像素 (K4 单轨的硬保证)。</summary>
    [LibraryImport("user32.dll")]
    internal static partial nint SetThreadDpiAwarenessContext(nint context);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool MoveWindow(nint hWnd, int x, int y, int w, int h, [MarshalAs(UnmanagedType.Bool)] bool repaint);

    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromPoint(POINT pt, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);
}
