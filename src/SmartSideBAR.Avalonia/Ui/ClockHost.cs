// 统一时钟宿主 (P0-1)。
// 借鉴 Class-Widgets-2 的 UnionUpdateTimer: 全应用只保留一个 DispatcherTimer,
// 秒级及以上的周期任务登记到这里, 由它统一驱动。
// 亚秒级(激光笔 80ms)与可重置的闲时计时(悬浮球)保持各自定时器 —— 它们不是固定节拍,
// 强行合并会改变语义。
using SmartSideBAR.Core.Scheduling;

namespace SmartSideBAR.Avalonia.Ui;

public static class ClockHost
{
    private static readonly ClockConsumers Consumers = new();
    private static readonly UnifiedClock Clock = new();
    private static global::Avalonia.Threading.DispatcherTimer? _timer;

    public static IReadOnlyDictionary<string, int>? ConsumerSnapshotForTests => null;

    /// <summary>启动主时钟(幂等)。</summary>
    public static void EnsureStarted()
    {
        if (_timer is not null) return;
        _timer = new global::Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000),
        };
        _timer.Tick += (_, _) =>
        {
            // 只在秒真正翻页时推进, 避免定时器抖动造成重复执行
            if (!Clock.ShouldTick(DateTimeOffset.Now)) return;
            Consumers.Pump(Environment.TickCount64);
        };
        _timer.Start();
    }

    /// <summary>登记一个周期消费者; 同名重复登记会覆盖。</summary>
    public static void Register(string name, int intervalMs, Action handler)
    {
        Consumers.Register(name, intervalMs, handler);
        EnsureStarted();
    }

    public static bool Unregister(string name) => Consumers.Unregister(name);

    /// <summary>仅供测试: 手动推进一拍。</summary>
    public static int PumpForTests(long nowMs) => Consumers.Pump(nowMs);
}
