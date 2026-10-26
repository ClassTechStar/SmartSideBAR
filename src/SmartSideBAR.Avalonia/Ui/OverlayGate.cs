// 覆盖层占位门, 对应 v1.2 渲染层的 window.__keepSidebarOpen。
// 截图/批注覆盖层一出现，指针就不在侧栏窗口里，5 秒自动收起会把刚打开的截图面板关掉。
// 覆盖层存活期间抑制自动收起，关掉后恢复正常。
namespace SmartSideBAR.Avalonia.Ui;

internal static class OverlayGate
{
    private static int _open;

    public static bool AnyOpen => Volatile.Read(ref _open) > 0;

    public static void Enter() => Interlocked.Increment(ref _open);

        /// <summary>全部覆盖层关闭后触发, 侧栏据此刷新"最近文件"等状态。</summary>
    public static event Action? AllClosed;

    public static void Exit()
    {
        if (Interlocked.Decrement(ref _open) == 0) AllClosed?.Invoke();
    }
}
