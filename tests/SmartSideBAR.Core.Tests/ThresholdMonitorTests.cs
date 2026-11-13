// 阈值告警的边沿检测 / 连续命中 / 迟滞（借鉴 SidebarDiagnostics BaseMetric）。
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Core.Tests;

public class ThresholdMonitorTests
{
    [Fact]
    public void 单次超限即触发且只触发一次()
    {
        var m = new ThresholdMonitor(80);
        Assert.Equal(ThresholdTransition.Raised, m.Push(90));
        Assert.Equal(ThresholdTransition.None, m.Push(95)); // 持续超限不重复触发
        Assert.True(m.IsRaised);
    }

    [Fact]
    public void 回落到阈值下清除告警()
    {
        var m = new ThresholdMonitor(80);
        m.Push(90);
        Assert.Equal(ThresholdTransition.Cleared, m.Push(70));
        Assert.False(m.IsRaised);
        Assert.Equal(ThresholdTransition.None, m.Push(60)); // 持续正常不重复清除
    }

    [Fact]
    public void 连续N次才触发()
    {
        var m = new ThresholdMonitor(80, requiredHits: 3);
        Assert.Equal(ThresholdTransition.None, m.Push(90));
        Assert.Equal(ThresholdTransition.None, m.Push(90));
        Assert.Equal(ThresholdTransition.Raised, m.Push(90));
    }

    [Fact]
    public void 中途回落重新计数()
    {
        var m = new ThresholdMonitor(80, requiredHits: 3);
        m.Push(90);
        m.Push(70);
        m.Push(90);
        Assert.Equal(ThresholdTransition.None, m.Push(90));
    }

    [Fact]
    public void 迟滞带内保持状态不抖()
    {
        // 阈值 80，迟滞 10 → 70~80 之间既不清除也不解除告警
        var m = new ThresholdMonitor(80, requiredHits: 1, hysteresis: 10);
        m.Push(90);
        Assert.Equal(ThresholdTransition.None, m.Push(75));
        Assert.True(m.IsRaised);
        Assert.Equal(ThresholdTransition.Cleared, m.Push(69));
    }

    [Fact]
    public void Reset_清空状态与计数()
    {
        var m = new ThresholdMonitor(80, requiredHits: 2);
        m.Push(90);
        m.Push(90);
        Assert.True(m.IsRaised);
        m.Reset();
        Assert.False(m.IsRaised);
        Assert.Equal(ThresholdTransition.None, m.Push(90)); // 重新累计
    }

    [Fact]
    public void 最小命中数强制为1()
    {
        var m = new ThresholdMonitor(1, requiredHits: 0);
        Assert.Equal(1, m.RequiredHits);
        Assert.Equal(ThresholdTransition.Raised, m.Push(1));
    }
}
