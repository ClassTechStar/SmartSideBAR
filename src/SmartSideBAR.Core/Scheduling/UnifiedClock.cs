// 统一时钟: 把散落的秒级定时器收敛成一个时间源。
// 借鉴 Class-Widgets-2 的 UnionUpdateTimer —— 底层高频轮询, 只在"秒发生变化"时对外广播一次。
using System.Diagnostics;

namespace SmartSideBAR.Core.Scheduling;

/// <summary>
/// 秒级节拍判定。纯逻辑, 不依赖 UI 线程, 便于单测。
/// </summary>
public sealed class UnifiedClock
{
    private long? _lastSecond;

    /// <summary>喂入当前时刻, 若秒已翻页则返回 true(应当广播一次 tick)。</summary>
    public bool ShouldTick(DateTimeOffset now)
    {
        var second = now.ToUnixTimeSeconds();
        if (_lastSecond == second) return false;
        _lastSecond = second;
        return true;
    }

    /// <summary>重置(用于测试或休眠恢复后避免误判)。</summary>
    public void Reset() => _lastSecond = null;

    /// <summary>供消费方自节流: 距上次满足间隔则返回 true。</summary>
    public static bool Due(long lastRunMs, long nowMs, int intervalMs)
        => intervalMs <= 0 || nowMs - lastRunMs >= intervalMs;
}

/// <summary>
/// 秒级节拍的消费者登记。消费方按名字注册自己的最小间隔, 由时钟统一驱动。
/// </summary>
public sealed class ClockConsumers
{
    private readonly Dictionary<string, (int IntervalMs, Action Handler)> _handlers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, long> _lastRun = new(StringComparer.Ordinal);
    private long _epochMs;

    public void Register(string name, int intervalMs, Action handler)
    {
        _handlers[name] = (Math.Max(0, intervalMs), handler);
        _lastRun.TryAdd(name, long.MinValue / 4);
    }

    public bool Unregister(string name)
    {
        _handlers.Remove(name);
        return _lastRun.Remove(name);
    }

    /// <summary>推进一个节拍, 返回本拍被触发的消费方数量。</summary>
    public int Pump(long nowMs)
    {
        _epochMs = nowMs;
        var fired = 0;
        foreach (var kv in _handlers)
        {
            var (interval, handler) = kv.Value;
            var last = _lastRun.TryGetValue(kv.Key, out var v) ? v : long.MinValue / 4;
            if (!UnifiedClock.Due(last, nowMs, interval)) continue;
            _lastRun[kv.Key] = nowMs;
            try
            {
                handler();
                fired++;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ClockConsumers] {kv.Key} 处理失败: {ex.Message}");
            }
        }
        return fired;
    }
}
