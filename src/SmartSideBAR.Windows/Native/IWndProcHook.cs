// WndProc 子类化钩子抽象 (单测注入假件)
namespace SmartSideBAR.Windows.Native;

/// <summary>
/// WndProc 回调委托。返回值: handled=true 时作为窗口过程结果直接返回; false 时继续传递给原过程。
/// </summary>
public delegate nint WndProcCallback(nint hwnd, uint msg, nuint wParam, nint lParam, ref bool handled);

public interface IWndProcHook
{
    /// <summary>
    /// 对 hwnd 安装钩子; 必须在窗口所属 UI 线程调用。可对同一 hwnd 叠加多个回调。
    /// 返回 false 表示子类化失败 (功能会静默失效, 调用方应记录/提示)。
    /// </summary>
    bool Add(nint hwnd, WndProcCallback callback);

    /// <summary>移除该 hwnd 上由 Add 安装的 callback; 最后一个移除时恢复原窗口过程。</summary>
    void Remove(nint hwnd, WndProcCallback callback);
}
