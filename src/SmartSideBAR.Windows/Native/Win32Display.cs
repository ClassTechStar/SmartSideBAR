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
}
