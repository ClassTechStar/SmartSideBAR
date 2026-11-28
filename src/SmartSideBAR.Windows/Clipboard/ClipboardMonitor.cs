// 剪贴板监听 (方案 §5.3, W3-4): AddClipboardFormatListener 消息驱动,
// 不用老式 SetClipboardViewer 链 (不吞其他程序事件)。
// 读剪贴板: CF_UNICODETEXT (文本) / CF_HDROP (文件列表); 图片格式暂缓 (P3)。
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SmartSideBAR.Core.Clipboard;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Windows.Clipboard;

/// <summary>监听捕获的原始数据 (未过隐私闸门, 由调用方判定)。</summary>
public sealed record RawClipboardData(
    ClipboardEntryKind Kind,
    string Text,
    IReadOnlyList<string> Paths,
    string ForegroundProcess);

public sealed class ClipboardMonitor : IWndProcHook, IDisposable
{
    private const uint WM_CLIPBOARDUPDATE = 0x031D;
    private nint _hwnd;
    private bool _listening;
    private WndProcCallback? _callback;

    /// <summary>剪贴板更新 (UI 线程触发 —— WndProc 所在线程)。</summary>
    public event Action<RawClipboardData>? Updated;

    /// <summary>挂到侧栏窗口的 WndProc 链并注册剪贴板格式监听。</summary>
    public bool Start(nint hwnd)
    {
        if (_listening) return true;
        _hwnd = hwnd;
        if (!AddClipboardFormatListener(hwnd))
        {
            return false;
        }
        _listening = true;
        _callback = OnWndProc;
        return ((IWndProcHook)this).Add(hwnd, _callback);
    }

    bool IWndProcHook.Add(nint hwnd, WndProcCallback callback)
    {
        // 剪贴板监听是广播消息, 不占 WndProc 槽位 —— 接口兼容占位
        _callback = callback;
        return true;
    }

    void IWndProcHook.Remove(nint hwnd, WndProcCallback callback) { /* 无槽位 */ }

    private nint OnWndProc(nint hwnd, uint msg, nuint wParam, nint lParam, ref bool handled)
    {
        if (msg != WM_CLIPBOARDUPDATE || hwnd != _hwnd) return 0;
        handled = true;
        var data = ReadClipboard();
        if (data is not null) Updated?.Invoke(data);
        return 0;
    }

    /// <summary>读取剪贴板当前内容 (文本或文件列表)。无法读取返回 null。</summary>
    public RawClipboardData? ReadClipboard()
    {
        // 剪贴板可能被其他进程短暂占用: 重试 5 次 x 20ms
        var opened = false;
        for (var attempt = 0; attempt < 5 && !opened; attempt++)
        {
            opened = OpenClipboard(_hwnd);
            if (!opened) Thread.Sleep(20);
        }
        if (!opened) return null;
        try
        {
            var fgHwnd = GetForegroundWindow();
            var fgProc = GetWindowProcessName(fgHwnd);

            // 文件 (CF_HDROP = 15)
            if (IsClipboardFormatAvailable(15))
            {
                var hDrop = GetClipboardData(15);
                if (hDrop != 0)
                {
                    var paths = DragQueryFileAll(hDrop);
                    if (paths.Count > 0)
                        return new RawClipboardData(ClipboardEntryKind.Files,
                            string.Join(Environment.NewLine, paths), paths, fgProc);
                }
            }

            // 文本 (CF_UNICODETEXT = 13)
            if (IsClipboardFormatAvailable(13))
            {
                var h = GetClipboardData(13);
                if (h != 0)
                {
                    var ptr = GlobalLock(h);
                    if (ptr != 0)
                    {
                        try
                        {
                            var text = Marshal.PtrToStringUni(ptr);
                            if (text is not null)
                                return new RawClipboardData(ClipboardEntryKind.Text, text, [], fgProc);
                        }
                        finally { GlobalUnlock(h); }
                    }
                }
            }
            return null;
        }
        finally { CloseClipboard(); }
    }

    public void Dispose()
    {
        if (_listening && _hwnd != 0)
        {
            try { RemoveClipboardFormatListener(_hwnd); } catch { }
        }
        _listening = false;
    }

    private static string GetWindowProcessName(nint hwnd)
    {
        try
        {
            _ = GetWindowThreadProcessId(hwnd, out var pid);
            var p = Process.GetProcessById((int)pid);
            return p.ProcessName;
        }
        catch { return ""; }
    }

    private static List<string> DragQueryFileAll(nint hDrop)
    {
        var paths = new List<string>();
        var count = (int)DragQueryFile(hDrop, 0xFFFFFFFFu, null, 0);
        for (var i = 0; i < count; i++)
        {
            var len = (int)DragQueryFile(hDrop, unchecked((uint)i), null, 0);
            var buf = Marshal.AllocCoTaskMem((len + 1) * 2);
            try
            {
                var n = DragQueryFile(hDrop, unchecked((uint)i), buf, (uint)((len + 1) * 2));
                paths.Add(Marshal.PtrToStringUni(buf, (int)n) ?? "");
            }
            finally { Marshal.FreeCoTaskMem(buf); }
        }
        return paths;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenClipboard(nint hWndNewOwner);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseClipboard();
    [DllImport("user32.dll")]
    private static extern nint GetClipboardData(uint uFormat);
    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(nint hDrop, uint iFile, nint? lpszFile, uint cch);
    [DllImport("kernel32.dll")]
    private static extern nint GlobalLock(nint hMem);
    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalUnlock(nint hMem);
}
