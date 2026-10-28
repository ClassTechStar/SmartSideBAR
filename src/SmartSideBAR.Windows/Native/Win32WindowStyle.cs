// 窗口扩展样式：置顶但不抢焦点 (P1-3)。
// 借鉴 CyreneCompass 的 TOPMOST + WS_EX_NOACTIVATE：
// 悬浮球/覆盖层浮在 PPT 放映、UWP 与普通窗口之上，但不把键盘焦点从教师当前输入处抢走。
using System.Runtime.InteropServices;

namespace SmartSideBAR.Windows.Native;

public static partial class Win32WindowStyle
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>给窗口加上 WS_EX_NOACTIVATE（点击时不激活窗口）。失败返回 false。</summary>
    public static bool SetNoActivate(nint hwnd)
    {
        if (hwnd == 0) return false;
        try
        {
            var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            if ((style & WS_EX_NOACTIVATE) != 0) return true;
            var updated = SetWindowLongPtr(hwnd, GWL_EXSTYLE, new nint(style | WS_EX_NOACTIVATE));
            return updated.ToInt64() != 0 || Marshal.GetLastWin32Error() == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>移除 WS_EX_NOACTIVATE（需要输入焦点时恢复）。</summary>
    public static bool ClearNoActivate(nint hwnd)
    {
        if (hwnd == 0) return false;
        try
        {
            var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            if ((style & WS_EX_NOACTIVATE) == 0) return true;
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, new nint(style & ~WS_EX_NOACTIVATE));
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>是否带工具窗口样式（不显示在任务栏/Alt+Tab）。</summary>
    public static bool HasToolWindow(nint hwnd)
    {
        if (hwnd == 0) return false;
        try
        {
            var style = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
            return (style & WS_EX_TOOLWINDOW) != 0;
        }
        catch
        {
            return false;
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}
