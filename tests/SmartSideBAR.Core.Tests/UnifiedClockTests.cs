// 统一时钟 (P0-1): 秒级节拍判定与消费方节流。
using SmartSideBAR.Core.Scheduling;

namespace SmartSideBAR.Core.Tests;

public class UnifiedClockTests
{
    private static DateTimeOffset At(int second) => new(2026, 10, 27, 8, 0, second, TimeSpan.Zero);

    [Fact]
    public void ShouldTick_秒未变时不重复触发()
    {
        var clock = new UnifiedClock();
        Assert.True(clock.ShouldTick(At(0)));
        Assert.False(clock.ShouldTick(At(0)));
        Assert.True(clock.ShouldTick(At(1)));
    }

    [Fact]
    public void Reset_后重新判定()
    {
        var clock = new UnifiedClock();
        Assert.True(clock.ShouldTick(At(5)));
        clock.Reset();
        Assert.True(clock.ShouldTick(At(5)));
    }

    [Fact]
    public void Due_间隔未到不触发()
    {
        Assert.False(UnifiedClock.Due(0, 500, 1000));
        Assert.True(UnifiedClock.Due(0, 1000, 1000));
        // 间隔 <= 0 视为每拍都跑
        Assert.True(UnifiedClock.Due(0, 1, 0));
    }

    [Fact]
    public void Consumers_按各自间隔触发()
    {
        var consumers = new ClockConsumers();
        var fast = 0;
        var slow = 0;
        consumers.Register("fast", 1000, () => fast++);
        consumers.Register("slow", 5000, () => slow++);

        Assert.Equal(2, consumers.Pump(0));
        // 第二秒: 仅 fast 到达间隔
        Assert.Equal(1, consumers.Pump(1000));
        Assert.Equal(2, fast);
        Assert.Equal(1, slow);
    }

    [Fact]
    public void Consumers_处理方异常不影响其它消费方()
    {
        var consumers = new ClockConsumers();
        var ok = 0;
        consumers.Register("boom", 0, () => throw new InvalidOperationException("x"));
        consumers.Register("ok", 0, () => ok++);

        Assert.Equal(1, consumers.Pump(0));
        Assert.Equal(1, ok);
    }
}
