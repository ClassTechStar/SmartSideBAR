// 窗口管理互操作 (方案 §8, W9-10): 枚举/摆位/置顶/托盘化/锁定/专注模式/桌面图标。
// 纪律: 全部显式操作 + 可撤销 (工具自己记住修改前的状态) + 调用方 Toast 反馈。
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SmartSideBAR.Windows.DesktopEnhance;

/// <summary>枚举出的可见顶层窗口。</summary>
public sealed record NativeWindowInfo(
    nint Hwnd, string ExeName, string Title, int X, int Y, int W, int H,
    bool Topmost, int MonitorIndex);

/// <summary>布局快照条目 (对应 Core.Layout.WindowLayoutEntry 的互操作形态)。</summary>
public sealed record NativeWindowSnapshot(string ExeName, string Title, int X, int Y, int W, int H, int MonitorIndex);

public sealed class NativeWindowTools
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x80;
    private const int WS_EX_LAYERED = 0x80000;
    private const int SWP_NOZORDER = 0x0004;
    private const int SWP_NOACTIVATE = 0x0010;
    private const nint HWND_TOPMOST = -1;
    private const nint HWND_NOTOPMOST = -2;
    private const uint SC_MOVE = 0xF010;
    private const uint MF_BYCOMMAND = 0x0;

    // ---- 枚举 ----

    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hWnd, System.Text.StringBuilder text, int count);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hWnd, out RECT rect);
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int attr, out int value, int size);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint hWnd, int index);

    private struct RECT { public int Left, Top, Right, Bottom; }

    /// <summary>枚举可见、非工具窗、非本进程的顶层窗口。</summary>
    public List<NativeWindowInfo> EnumerateVisibleWindows(uint selfPid)
    {
        var list = new List<NativeWindowInfo>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            if (IsCloaked(hwnd)) return true;
            // 回调 lambda 的 _ 参数是 nint (lParam), 与 _ = discard 冲突 → 直接不赋值
            GetWindowThreadProcessId(hwnd, out var pid);
            if (pid == selfPid) return true;
            var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;
            var title = GetWindowTitle(hwnd);
            if (title.Length == 0) return true;
            var exe = GetExeName(pid);
            if (exe.Length == 0) return true;
            var rect = new RECT();
            GetWindowRect(hwnd, out rect);
            list.Add(new NativeWindowInfo(hwnd, exe, title,
                rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top,
                (GetWindowLong(hwnd, GWL_EXSTYLE) & 8) != 0 /* WS_EX_TOPMOST = 8 */, MonitorIndexOf(hwnd)));
            return true;
        }, nint.Zero);
        return list;
    }

    private static bool IsCloaked(nint hwnd)
    {
        try
        {
            var cloaked = 0;
            _ = DwmGetWindowAttribute(hwnd, 14 /* DWMWA_CLOAKED */, out cloaked, sizeof(int));
            return cloaked != 0;
        }
        catch { return false; }
    }

    private static string GetWindowTitle(nint hwnd)
    {
        var sb = new System.Text.StringBuilder(512);
        _ = GetWindowText(hwnd, sb, 512);
        return sb.ToString().Trim();
    }

    private static string GetExeName(uint pid)
    {
        try
        {
            var p = Process.GetProcessById((int)pid);
            return Path.GetFileNameWithoutExtension(p.ProcessName);
        }
        catch { return ""; }
    }

    private static int MonitorIndexOf(nint hwnd) => 0; // 多屏序号在调用方按 rect 中心点判, 此处保守 0

    // ---- 摆位 / 快照恢复 ----

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hWnd, nint after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int cmd);

    public bool MoveWindow(nint hwnd, int x, int y, int w, int h, bool restoreFirst = true)
    {
        if (restoreFirst) ShowWindow(hwnd, 9); // SW_RESTORE
        return SetWindowPos(hwnd, nint.Zero, x, y, w, h, SWP_NOZORDER | SWP_NOACTIVATE);
    }

    // ---- 置顶切换 ----

    public bool ToggleTopmost(nint hwnd)
    {
        var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        var isTop = (ex & 8) != 0;
        var ok = SetWindowPos(hwnd, isTop ? HWND_NOTOPMOST : HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOZORDER | SWP_NOACTIVATE | 0x0001 /* NOMOVE */ | 0x0002 /* NOSIZE */);
        return ok && !isTop;
    }

    // ---- 最小化到托盘 (隐藏 + 清单可恢复; 退出还原由调用方保证) ----

    private readonly List<(nint Hwnd, string Title)> _hidden = [];

    public int HideToTray(nint hwnd, string title)
    {
        if (!ShowWindow(hwnd, 0)) return _hidden.Count; // SW_HIDE
        _hidden.Add((hwnd, title));
        return _hidden.Count;
    }

    public int RestoreHidden()
    {
        var count = _hidden.Count;
        foreach (var (hwnd, _) in _hidden) ShowWindow(hwnd, 5); // SW_SHOW
        _hidden.Clear();
        return count;
    }

    // ---- 锁定窗口 (去 SC_MOVE, 可逆) ----

    [DllImport("user32.dll")]
    private static extern nint GetSystemMenu(nint hWnd, bool bRevert);
    [DllImport("user32.dll")]
    private static extern bool RemoveMenu(nint hMenu, uint position, uint flags);

    private readonly List<nint> _lockedMenus = [];

    public bool LockWindow(nint hwnd)
    {
        var menu = GetSystemMenu(hwnd, false);
        if (menu == 0) return false;
        var ok = RemoveMenu(menu, SC_MOVE, MF_BYCOMMAND);
        if (ok) _lockedMenus.Add(hwnd);
        return ok;
    }

    public void UnlockAll()
    {
        foreach (var hwnd in _lockedMenus)
        {
            _ = GetSystemMenu(hwnd, true); // bRevert=true 还原系统菜单
        }
        _lockedMenus.Clear();
    }

    // ---- 专注模式 (其他窗口置灰, 可逆) ----

    private readonly Dictionary<nint, (int ExStyle, nint Hwnd)> _dimmed = [];

    public int DimOthers(nint keepHwnd, IEnumerable<NativeWindowInfo> others, byte alpha = 64)
    {
        var count = 0;
        foreach (var w in others)
        {
            if (w.Hwnd == keepHwnd || _dimmed.ContainsKey(w.Hwnd)) continue;
            var ex = GetWindowLong(w.Hwnd, GWL_EXSTYLE);
            if ((ex & WS_EX_LAYERED) == 0)
            {
                SetWindowLong(w.Hwnd, GWL_EXSTYLE, ex | WS_EX_LAYERED);
                _dimmed[w.Hwnd] = (ex, w.Hwnd);
            }
            if (SetLayeredWindowAttributes(w.Hwnd, 0, alpha, 0x2 /* LWA_ALPHA */)) count++;
        }
        return count;
    }

    public int UndimAll()
    {
        foreach (var kv in _dimmed)
        {
            var cur = GetWindowLong(kv.Value.Hwnd, GWL_EXSTYLE);
            SetWindowLong(kv.Value.Hwnd, GWL_EXSTYLE, (cur & ~WS_EX_LAYERED) | (kv.Value.ExStyle & WS_EX_LAYERED));
        }
        var n = _dimmed.Count;
        _dimmed.Clear();
        return n;
    }

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(nint hwnd, uint crKey, byte bAlpha, uint dwFlags);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(nint hWnd, int index, int value);

    // ---- 桌面图标显隐 (Progman → SHELLDLL_DefView → SysListView32) ----

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindow(string cls, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint FindWindowEx(nint parent, nint after, string cls, string? title);

    private bool _iconsHidden;

    public bool ToggleDesktopIcons()
    {
        var progman = FindWindow("Progman", null);
        if (progman == 0) return false;
        var defView = FindWindowEx(progman, nint.Zero, "SHELLDLL_DefView", null);
        if (defView == nint.Zero)
        {
            // WorkerW 分层兜底 (某些桌面配置 DefView 挂在 WorkerW 下)
            var worker = nint.Zero;
            do
            {
                worker = FindWindowEx(nint.Zero, worker, "WorkerW", null);
                defView = FindWindowEx(worker, nint.Zero, "SHELLDLL_DefView", null);
            } while (defView == nint.Zero && worker != nint.Zero);
        }
        if (defView == nint.Zero) return false;
        var list = FindWindowEx(defView, nint.Zero, "SysListView32", null);
        if (list == nint.Zero) return false;
        _iconsHidden = !_iconsHidden;
        return ShowWindow(list, _iconsHidden ? 0 : 5);
    }
}
