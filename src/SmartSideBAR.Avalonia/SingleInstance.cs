// 单实例守卫, 对应 v1.2 main.ts 的 app.requestSingleInstanceLock()。
// 没有它的话双击两次图标会起两个进程, 两个 AppBar 抢同一条屏幕边缘,
// 热键注册互相抢注、托盘出现两个图标。这里用命名互斥量判重，再用命名事件唤醒已有实例。
using Avalonia.Threading;

namespace SmartSideBAR.Avalonia;

internal static class SingleInstance
{
    private const string MutexName = @"Local\SmartSideBAR.Instance.v1";
    private const string EventName = @"Local\SmartSideBAR.Activate.v1";
    // 看门狗重启标记: 旧实例写、新实例读 —— 新进程启动时旧实例可能尚未完全退出,
    // 有标记则等待重试抢占, 避免新旧双双退出 (应用凭空消失)
    private static readonly string RestartMarkerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SmartSideBAR", "restart.lock");
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
            _event = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
            if (TryAcquireOnce()) return true;

            // 重启等待: 看门狗刚重启的场景, 旧实例还在退出流程中 ——
            // 重启标记新鲜 (<10s) 时最多等 8s 重试抢占, 拿到即作为主实例继续
            if (!IsRestartMarkerFresh()) return false;
            for (var i = 0; i < 16; i++)
            {
                Thread.Sleep(500);
                if (TryAcquireOnce())
                {
                    TryDeleteRestartMarker();
                    return true;
                }
            }
            return false; // 等待超时仍抢不到 → 按已有实例处理 (Signal 后退出)
        }
        catch
        {
            return true; // 极端情况下不因判重失败而拒绝启动
        }
    }

    /// <summary>单次抢占: 互斥量 + 回环端口双保险都拿到才算主实例。</summary>
    private static bool TryAcquireOnce()
    {
        _mutex?.Dispose(); // 未持有他人互斥, 释放自己上一轮的句柄 (含放弃所有权)
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew) return false;
        // 互斥量说是"新建"，但不同完整性级别（普通用户 / 管理员）会各自创建成功，
        // 再用回环端口兜一次：抢不到端口说明已有一个实例在跑。
        if (!TryHoldProbePort()) return false;
        return true;
    }

    /// <summary>重启标记是否新鲜 (10 秒内)。</summary>
    private static bool IsRestartMarkerFresh()
    {
        try
        {
            if (!File.Exists(RestartMarkerPath)) return false;
            return DateTimeOffset.Now - File.GetLastWriteTime(RestartMarkerPath) < TimeSpan.FromSeconds(10);
        }
        catch { return false; }
    }

    private static void TryDeleteRestartMarker()
    {
        try { File.Delete(RestartMarkerPath); } catch { /* 尽力而为 */ }
    }

    /// <summary>看门狗重启前调用: 落标记, 让新进程知道要等待旧实例退出。</summary>
    public static void MarkRestart()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RestartMarkerPath)!);
            File.WriteAllText(RestartMarkerPath, DateTimeOffset.Now.ToString("O"));
        }
        catch { /* 尽力而为 */ }
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
