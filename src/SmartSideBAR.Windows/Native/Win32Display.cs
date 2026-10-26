// 显示器/DPI 公共助手 (UI 层与互操作层的共享工具)
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

    /// <summary>主屏完整矩形 (物理像素, 含任务栏), 全屏覆盖层用。</summary>
    public static (int X, int Y, int W, int H) PrimaryMonitorRectPx()
    {
        var hMonitor = MonitorFromPoint(new NativePoint { x = 0, y = 0 }, 1 /*MONITOR_DEFAULTTOPRIMARY*/);
        var mi = new MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
        if (!GetMonitorInfo(hMonitor, ref mi))
        {
            return (0, 0, 1920, 1080);
        }
        return (mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.W, mi.rcMonitor.H);
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

    /// <summary>
    /// 按索引取第 n 块显示器的工作区(物理像素), 索引越界或查询失败时回退主屏。
    /// P1-5: 对应配置项 display.monitorIndex (借鉴 ClassIsland 的 WindowDockingMonitorIndex)。
    /// </summary>
    public static (int X, int Y, int W, int H) WorkAreaByIndex(int index)
    {
        if (index < 0) return PrimaryWorkAreaPx();
        var areas = EnumerateWorkAreas();
        if (areas.Count == 0 || index >= areas.Count) return PrimaryWorkAreaPx();
        return areas[index];
    }

    /// <summary>枚举所有显示器工作区(物理像素), 顺序与系统枚举顺序一致。</summary>
    public static List<(int X, int Y, int W, int H)> EnumerateWorkAreas()
    {
        var result = new List<(int, int, int, int)>();
        try
        {
            EnumDisplayMonitors(nint.Zero, nint.Zero, (hMonitor, _, _, _) =>
            {
                var mi = new MONITORINFO
                {
                    cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>(),
                };
                if (GetMonitorInfo(hMonitor, ref mi))
                    result.Add((mi.rcWork.Left, mi.rcWork.Top, mi.rcWork.W, mi.rcWork.H));
                return true; // 继续枚举
            }, nint.Zero);
        }
        catch
        {
            return result;
        }
        return result;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumDisplayMonitors(
        nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, nint lprcMonitor, nint dwData);

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
