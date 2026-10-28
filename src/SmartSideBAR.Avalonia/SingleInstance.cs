// 单实例守卫, 对应 v1.2 main.ts 的 app.requestSingleInstanceLock()。
// 没有它的话双击两次图标会起两个进程, 两个 AppBar 抢同一条屏幕边缘,
// 热键注册互相抢注、托盘出现两个图标。这里用命名互斥量判重，再用命名事件唤醒已有实例。
using Avalonia.Threading;

namespace SmartSideBAR.Avalonia;

internal static class SingleInstance
{
    private const string MutexName = @"Local\SmartSideBAR.Instance.v1";
    private const string EventName = @"Local\SmartSideBAR.Activate.v1";
    // P2-6: 回环探测端口（借鉴 CyreneCompass）。命名互斥量在跨完整性级别
    //（普通用户 vs 管理员）时可能各自创建成功，用回环端口再兜一次。
    private const int ProbePort = 51871;

    private static Mutex? _mutex;
    private static EventWaitHandle? _event;
    private static System.Net.Sockets.TcpListener? _probe;

    /// <summary>首次启动返回 true；已有实例返回 false（调用方应 Signal 后退出）。</summary>
    public static bool Acquire()
    {
        try
        {
            _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
            _event = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            // 互斥量说是"新建"，但不同完整性级别（普通用户 / 管理员）会各自创建成功，
            // 再用回环端口兜一次：抢不到端口说明已有一个实例在跑。
            if (createdNew && !TryHoldProbePort()) createdNew = false;
            return createdNew;
        }
        catch
        {
            return true; // 极端情况下不因判重失败而拒绝启动
        }
    }

    /// <summary>尝试占用回环探测端口并保持占用（进程退出即释放）。占用成功返回 true。</summary>
    private static bool TryHoldProbePort()
    {
        try
        {
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, ProbePort)
            {
                ExclusiveAddressUse = true,
            };
            listener.Start();
            _probe = listener;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>释放探测端口（退出前调用）。</summary>
    public static void ReleaseProbe()
    {
        try { _probe?.Stop(); } catch { /* 停不掉不影响退出 */ }
        _probe = null;
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
