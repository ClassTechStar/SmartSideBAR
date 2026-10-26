// 单实例守卫, 对应 v1.2 main.ts 的 app.requestSingleInstanceLock()。
// 没有它的话双击两次图标会起两个进程, 两个 AppBar 抢同一条屏幕边缘,
// 热键注册互相抢注、托盘出现两个图标。这里用命名互斥量判重，再用命名事件唤醒已有实例。
using Avalonia.Threading;

namespace SmartSideBAR.Avalonia;

internal static class SingleInstance
{
    private const string MutexName = @"Local\SmartSideBAR.Instance.v1";
    private const string EventName = @"Local\SmartSideBAR.Activate.v1";

    private static Mutex? _mutex;
    private static EventWaitHandle? _event;

    /// <summary>首次启动返回 true；已有实例返回 false（调用方应 Signal 后退出）。</summary>
    public static bool Acquire()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            _event = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            return createdNew;
        }
        catch
        {
            return true; // 极端情况下不因判重失败而拒绝启动
        }
    }

    /// <summary>唤醒已运行的实例（第二次启动时调用）。</summary>
    public static void Signal()
    {
        try { _event?.Set(); } catch { /* 已有实例可能正好在退出 */ }
    }

    /// <summary>在后台线程等待唤醒信号，回调切到 UI 线程执行。</summary>
    public static void Listen(Action onActivate)
    {
        if (_event is null) return;
        var thread = new Thread(() =>
        {
            while (true)
            {
                try
                {
                    if (!_event.WaitOne()) continue;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                Dispatcher.UIThread.Post(onActivate);
            }
        })
        {
            IsBackground = true,
            Name = "SmartSideBAR.SingleInstance",
        };
        thread.Start();
    }
}
