// K1 通知处理的基础设施。
// 实现方式: SetWindowLongPtr(GWLP_WNDPROC) 经典子类化。方案 §5.3 原想用 Avalonia 的
// AddWndProcCallback, 但该 API 未公开(为 Avalonia 内部实现); 子类化等效且零 UI 框架耦合,
// 同一钩子可被 AppBar / 全局热键(WM_HOTKEY) / USB(WM_DEVICECHANGE) 共用 (Wave B 复用点)。
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace SmartSideBAR.Windows.Native;

[UnmanagedFunctionPointer(CallingConvention.StdCall)]
internal delegate nint WndProcThunk(nint hwnd, uint msg, nuint wParam, nint lParam);

public sealed partial class WndProcHook(ILogger<WndProcHook>? log = null) : IWndProcHook
{
    private const int GWLP_WNDPROC = -4;

    private static readonly Lock Gate = new();
    private static readonly Dictionary<nint, Subclass> Subclasses = [];

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr64(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static partial int GetWindowLong32(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static partial int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
    private static partial nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nuint wParam, nint lParam);

    public bool Add(nint hwnd, WndProcCallback callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        lock (Gate)
        {
            if (Subclasses.TryGetValue(hwnd, out var existing))
            {
                existing.AddCallback(callback);
                return true;
            }
            var prevProc = Environment.Is64BitProcess
                ? GetWindowLongPtr64(hwnd, GWLP_WNDPROC)
                : GetWindowLong32(hwnd, GWLP_WNDPROC);
            var subclass = new Subclass(prevProc);
            subclass.AddCallback(callback);
            Subclasses[hwnd] = subclass;
            var newProc = Marshal.GetFunctionPointerForDelegate(subclass.Thunk);
            // 子类化是"窗口消息路由"的唯一入口: 失败时热键/U 盘/AppBar 全部静默失效,
            // 必须把失败如实回报给调用方 (2026-11 审计: 原实现丢弃返回值, 用户无感)。
            var previous = Environment.Is64BitProcess
                ? SetWindowLongPtr64(hwnd, GWLP_WNDPROC, newProc)
                : SetWindowLong32(hwnd, GWLP_WNDPROC, unchecked((int)newProc));
            if (previous == 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 0)
                {
                    Subclasses.Remove(hwnd);
                    log?.LogWarning("[Hook] WndProc 子类化失败 (hwnd={Hwnd}, win32={Error}): 热键/U 盘/AppBar 通知将不可用",
                        hwnd, error);
                    return false;
                }
            }
            return true;
        }
    }

    public void Remove(nint hwnd, WndProcCallback callback)
    {
        lock (Gate)
        {
            if (!Subclasses.TryGetValue(hwnd, out var subclass)) return;
            if (!subclass.RemoveCallback(callback)) return;
            if (subclass.CallbackCount == 0)
            {
                _ = Environment.Is64BitProcess
                    ? SetWindowLongPtr64(hwnd, GWLP_WNDPROC, subclass.PrevProc)
                    : SetWindowLong32(hwnd, GWLP_WNDPROC, unchecked((int)subclass.PrevProc));
                Subclasses.Remove(hwnd);
            }
        }
    }

    private sealed class Subclass(nint prevProc)
    {
        private readonly object _gate = new();
        private List<WndProcCallback> _callbacks = [];
        public readonly WndProcThunk Thunk = Dispatch;

        public nint PrevProc { get; } = prevProc;

        public int CallbackCount { get { lock (_gate) return _callbacks.Count; } }

        public void AddCallback(WndProcCallback callback)
        {
            lock (_gate) _callbacks.Add(callback);
        }

        public bool RemoveCallback(WndProcCallback callback)
        {
            lock (_gate) return _callbacks.Remove(callback);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static nint Dispatch(nint hwnd, uint msg, nuint wParam, nint lParam)
        {
            // WndProc 在窗口线程被系统回调; 此处只读快照, 不持锁
            Subclass? subclass;
            lock (Gate)
            {
                if (!Subclasses.TryGetValue(hwnd, out subclass))
                    return DefWindowProc(hwnd, msg, wParam, lParam);
            }

            foreach (var callback in subclass.Snapshot())
            {
                try
                {
                    var handled = false;
                    var result = callback(hwnd, msg, wParam, lParam, ref handled);
                    if (handled) return result;
                }
                catch
                {
                    // 原生回调里抛托管异常 = 无堆栈无日志的进程崩溃 (审计结论):
                    // 单个回调失败降级为"未处理", 消息照常走 DefWindowProc
                }
            }
            return CallWindowProc(subclass.PrevProc, hwnd, msg, wParam, lParam);
        }

        private WndProcCallback[] Snapshot()
        {
            lock (_gate) return [.. _callbacks];
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
    private static partial nint DefWindowProc(nint hWnd, uint msg, nuint wParam, nint lParam);
}
