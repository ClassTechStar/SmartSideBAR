// P0 参数化批回归: DisplayNormalize 钳制 + 新配置键 roundtrip (方案 §3.1/§7.1)
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Tests;

public class DisplayNormalizeTests
{
    private static DisplayConfig Clamp(DisplayConfig c) => DisplayNormalize.Clamp(c);

    [Theory]
    [InlineData(4, 8)]       // 低于下限 → 钳到下限 (fallback 仅用于非有限值)
    [InlineData(8, 8)]
    [InlineData(22, 22)]
    [InlineData(40, 40)]
    [InlineData(99, 40)]     // 超上限 → 上限
    public void TriggerWidth_钳制在8到40(double input, double expected)
    {
        Assert.Equal(expected, Clamp(new DisplayConfig { TriggerWidthDip = input }).TriggerWidthDip);
    }

    [Fact]
    public void HoverDelay_与CollapseDelay_钳制()
    {
        var c = Clamp(new DisplayConfig { HoverExpandDelayMs = -5, CollapseDelaySec = 99 });
        Assert.Equal(0, c.HoverExpandDelayMs);
        Assert.Equal(30, c.CollapseDelaySec);
    }

    [Fact]
    public void RailWidth_与PanelWidth_钳制()
    {
        var c = Clamp(new DisplayConfig { RailWidthDip = 30, PanelWidthDip = 800 });
        Assert.Equal(56, c.RailWidthDip);
        Assert.Equal(520, c.PanelWidthDip);
    }

    [Fact]
    public void TopmostPolicy_非法值回退normal()
    {
        Assert.Equal("normal", Clamp(new DisplayConfig { TopmostPolicy = "hijack" }).TopmostPolicy);
        Assert.Equal("always", Clamp(new DisplayConfig { TopmostPolicy = "ALWAYS" }).TopmostPolicy);
        Assert.Equal("never", Clamp(new DisplayConfig { TopmostPolicy = "never" }).TopmostPolicy);
    }

    [Fact]
    public void 默认值与v1x行为兼容()
    {
        // 不设置任何新键 → 与 1.x 行为完全一致 (零迁移成本)
        var c = Clamp(new DisplayConfig());
        Assert.Equal(14, c.TriggerWidthDip);
        Assert.Equal(150, c.HoverExpandDelayMs);
        Assert.Equal(5, c.CollapseDelaySec);
        Assert.Equal("normal", c.TopmostPolicy);
        Assert.Equal(72, c.RailWidthDip);
        Assert.Equal(380, c.PanelWidthDip);
        Assert.Equal(28, c.IconDip);
        Assert.False(c.CollapseOnFocusLoss);
    }
}
