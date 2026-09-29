// Windows/Native/Win32Display.cs —— 显示器/DPI 公共助手 (UI 层与互操作层的共享工具)
using System.Runtime.InteropServices;

namespace SmartSideBAR.Windows.Native;

public static partial class Win32Display
{
    [LibraryImport("user32.dll")]
    private static partial uint GetDpiForWindow(nint hwnd);

    /// <summary>窗口 DPI 缩放因子 (96 = 1.0)。物理像素 ↔ DIP 的唯一换算依据 (K4 单轨)。</summary>
    public static double GetScaling(nint hwnd) =>
        hwnd != 0 ? GetDpiForWindow(hwnd) / 96.0 : 1.0;

    /// <summary>主屏工作区矩形 (物理像素, PMv2 上下文)。</summary>
    public static (int X, int Y, int W, int H) PrimaryWorkAreaPx()
    {
        var hMonitor = MonitorFromPoint(new NativePoint { x = 0, y = 0 }, 1 /*MONITOR_DEFAULTTOPRIMARY*/);
        var mi = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref mi))
        {
            return (0, 0, 1920, 1040);
        }
        return (mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.W, mi.rcWork.H);
    }

    /// <summary>R4: 从 HWND 获取所在显示器的工作区 (物理像素)。多显示器时悬浮球/dock 正确定位。</summary>
    public static (int X, int Y, int W, int H) WorkAreaFromHwnd(nint hwnd)
    {
        if (hwnd == 0) return PrimaryWorkAreaPx();
        var hMonitor = MonitorFromWindow(hwnd, 2 /*MONITOR_DEFAULTTONEAREST*/);
        var mi = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref mi))
        {
            return PrimaryWorkAreaPx();
        }
        return (mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.W, mi.rcWork.H);
    }

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromWindow(nint hwnd, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; public readonly int W => Right - Left; public readonly int H => Bottom - Top; }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint { public int x, y; }

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(NativePoint pt, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);
}
