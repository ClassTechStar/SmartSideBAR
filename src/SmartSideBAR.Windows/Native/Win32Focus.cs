// 前台焦点助手 (§3.2 收起归还焦点): GetForegroundWindow 快照 + 归还。
// 收起侧栏/悬浮球时把焦点还给用户正在用的窗口 —— "收起后完全释放焦点"。
using System.Runtime.InteropServices;

namespace SmartSideBAR.Windows.Native;

public static class Win32Focus
{
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);

    /// <summary>当前前台窗口句柄 (0 = 桌面/无前台)。</summary>
    public static nint GetForeground() => GetForegroundWindow();

    /// <summary>把焦点归还给指定窗口; 0 或失败静默 (尽力而为)。</summary>
    public static void ReturnTo(nint hwnd)
    {
        if (hwnd != 0) SetForegroundWindow(hwnd);
    }
}
