// 防抖闸门（借鉴 SidebarDiagnostics 首次向导的 CancellationTokenSource + Delay 防抖）。
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Core.Tests;

public class DebounceGateTests
{
    [Fact]
    public void 静默不足不执行()
    {
        var g = new DebounceGate(450);
        g.Trigger(1000);
        Assert.False(g.TryFire(1200));
        Assert.True(g.IsPending);
    }

    [Fact]
    public void 到达静默时长执行一次()
    {
        var g = new DebounceGate(450);
        g.Trigger(1000);
        Assert.True(g.TryFire(1450));
        Assert.False(g.TryFire(1500)); // 只消费一次
        Assert.False(g.IsPending);
    }

    [Fact]
    public void 连续触发会重置等待窗口()
    {
        var g = new DebounceGate(450);
        g.Trigger(1000);
        g.Trigger(1200);
        g.Trigger(1400);
        Assert.False(g.TryFire(1500));
        Assert.True(g.TryFire(1850));
    }

    [Fact]
    public void 没有待执行动作时不触发()
    {
        var g = new DebounceGate(100);
        Assert.False(g.TryFire(9999));
    }

    [Fact]
    public void Reset_取消待执行()
    {
        var g = new DebounceGate(100);
        g.Trigger(1000);
        g.Reset();
        Assert.False(g.IsPending);
        Assert.False(g.TryFire(2000));
    }

    [Fact]
    public void 零延迟立即触发()
    {
        var g = new DebounceGate(0);
        g.Trigger(1000);
        Assert.True(g.TryFire(1000));
    }

    [Fact]
    public void 负延迟按零处理()
    {
        var g = new DebounceGate(-5);
        Assert.Equal(0, g.DelayMs);
    }
}
