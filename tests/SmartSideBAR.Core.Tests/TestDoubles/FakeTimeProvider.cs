// 测试假件: 可手动推进的 TimeProvider (方案 §6.10 可测调度)。
namespace SmartSideBAR.Core.Tests.TestDoubles;

public sealed class FakeTimeProvider : TimeProvider
{
    private sealed class FakeTimer(FakeTimeProvider owner, TimerCallback callback, object? state)
        : ITimer
    {
        public DateTimeOffset NextDue;
        public TimeSpan Period = Timeout.InfiniteTimeSpan;
        public bool Disposed;

        public void Schedule(TimeSpan dueTime, TimeSpan period)
        {
            Period = period;
            NextDue = owner.UtcNow + (dueTime == Timeout.InfiniteTimeSpan ? TimeSpan.MaxValue : dueTime);
        }

        public bool Dispose(bool wait) { Disposed = true; return true; }
        public void Dispose() { Disposed = true; }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Schedule(dueTime, period);
            return true;
        }

        public void FireIfDue()
        {
            while (!Disposed && NextDue <= owner.UtcNow)
            {
                NextDue += Period == Timeout.InfiniteTimeSpan ? TimeSpan.MaxValue : Period;
                callback(state);
            }
        }
    }

    private readonly List<FakeTimer> _timers = [];
    private DateTimeOffset _utcNow = new(2026, 9, 6, 8, 0, 0, TimeSpan.Zero);

    public DateTimeOffset UtcNow => _utcNow;

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FakeTimer(this, callback, state);
        timer.Schedule(dueTime, period);
        _timers.Add(timer);
        return timer;
    }

    public void Advance(TimeSpan delta)
    {
        var target = _utcNow + delta;
        // 逐周期推进, 保证周期定时器按节奏触发而非一次性追赶
        while (_utcNow < target)
        {
            _utcNow += TimeSpan.FromMilliseconds(Math.Min(1000, (target - _utcNow).TotalMilliseconds));
            FireDue();
        }
        FireDue();
    }

    public void FireDue()
    {
        // 可能存在触发顺序敏感性, 反复扫描直到无到期
        for (var pass = 0; pass < 10; pass++)
        {
            var fired = false;
            foreach (var t in _timers.ToArray())
            {
                if (!t.Disposed && t.NextDue <= _utcNow)
                {
                    t.FireIfDue();
                    fired = true;
                }
            }
            if (!fired) break;
        }
    }
}
