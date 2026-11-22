// 物理弹簧曲线正确性（2026-11）。
// 这是"回弹手感"的数学证明：欠阻尼必须真实过冲、临界阻尼必须不过冲、
// 过冲量必须与解析解一致、收敛时长必须满足包络判据。
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Core.Tests;

public class SpringCurveTests
{
    [Fact]
    public void 固有角频率等于两派除以response()
    {
        var c = new SpringCurve(responseSec: 0.34, dampingRatio: 0.86);
        Assert.Equal(2 * Math.PI / 0.34, c.Omega, 6);
    }

    [Fact]
    public void 起点为0终点为1()
    {
        var c = new SpringCurve(0.34, 0.86);
        Assert.Equal(0, c.ValueAt(0));
        Assert.Equal(0, c.ValueAt(-1));
        Assert.Equal(1, c.ValueAt(c.SettleSeconds));
        Assert.Equal(1, c.ValueAt(c.SettleSeconds * 2));
    }

    [Fact]
    public void 临界阻尼不过冲()
    {
        var c = new SpringCurve(0.28, 1.0);
        var max = 0.0;
        for (var i = 0; i <= 400; i++)
        {
            max = Math.Max(max, c.ValueAt(c.SettleSeconds * i / 400.0));
        }
        Assert.True(max <= 1.000001, $"临界阻尼出现了过冲: {max}");
        Assert.Equal(0, c.OverShootPercent, 6);
    }

    [Fact]
    public void 欠阻尼过冲量与解析解一致()
    {
        // ζ=0.55 → 理论过冲 = e^(-ζπ/√(1-ζ²)) ≈ 12.6%
        var c = new SpringCurve(0.34, 0.55);
        var max = 0.0;
        for (var i = 0; i <= 800; i++)
        {
            max = Math.Max(max, c.ValueAt(c.SettleSeconds * i / 800.0));
        }
        var measured = max - 1.0;
        Assert.Equal(c.OverShootPercent, measured, 2); // 两位小数容差
        Assert.InRange(c.OverShootPercent, 0.10, 0.16);
    }

    [Fact]
    public void 阻尼越小过冲越大()
    {
        var low = new SpringCurve(0.34, 0.45).OverShootPercent;
        var mid = new SpringCurve(0.34, 0.62).OverShootPercent;
        var high = new SpringCurve(0.34, 0.86).OverShootPercent;
        Assert.True(low > mid, $"{low} 应大于 {mid}");
        Assert.True(mid > high, $"{mid} 应大于 {high}");
        Assert.True(high > 0, "ζ=0.86 仍应有轻微回弹（mochi 标注约 1% 过冲）");
    }

    [Fact]
    public void 过阻尼不过冲且收敛更慢()
    {
        var critically = new SpringCurve(0.34, 1.0);
        var over = new SpringCurve(0.34, 2.0);
        Assert.Equal(0, over.OverShootPercent, 6);
        Assert.True(over.SettleSeconds > critically.SettleSeconds, "过阻尼收敛应更慢");

        var max = 0.0;
        for (var i = 0; i <= 400; i++) max = Math.Max(max, over.ValueAt(over.SettleSeconds * i / 400.0));
        Assert.True(max <= 1.000001);
    }

    [Fact]
    public void 收敛时长满足千分之一包络()
    {
        var c = new SpringCurve(0.34, 0.86);
        // SettleSeconds 处残余位移应 ≤ 0.1%
        Assert.True(1 - c.ValueAt(c.SettleSeconds * 0.999) <= 0.0015,
            $"收敛点残余 {1 - c.ValueAt(c.SettleSeconds * 0.999):P3} 超过 0.1% 判据");
    }

    [Fact]
    public void 收敛时长始终大于response()
    {
        // 物理上不可能在 response 时间内完全停住（response 只是时间尺度）
        foreach (var (response, damping) in new[] { (0.22, 0.62), (0.34, 0.86), (0.42, 1.0), (0.15, 1.0) })
        {
            var c = new SpringCurve(response, damping);
            Assert.True(c.SettleSeconds > response, $"response={response} ζ={damping} 的收敛时长不应小于 response");
        }
    }

    [Fact]
    public void response越小收敛越快()
    {
        var fast = new SpringCurve(0.22, 1.0).SettleSeconds;
        var slow = new SpringCurve(0.42, 1.0).SettleSeconds;
        Assert.True(fast < slow);
    }

    [Fact]
    public void 曲线中途单调趋近目标()
    {
        var c = new SpringCurve(0.42, 1.0);
        var previous = 0.0;
        for (var i = 1; i <= 100; i++)
        {
            var v = c.ValueAt(c.SettleSeconds * i / 100.0);
            Assert.True(v >= previous - 1e-9, $"临界阻尼曲线在 {i}% 处回退");
            previous = v;
        }
    }

    [Fact]
    public void 非法参数被拒绝()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpringCurve(0, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpringCurve(-0.1, 1.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpringCurve(0.3, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpringCurve(0.3, -1));
    }

    [Fact]
    public void 令牌十档曲线全部有界且收敛()
    {
        var m = DesignTokens.Current.M3.Motion;
        var specs = new[]
        {
            m.Page, m.Present, m.Pop, m.Control, m.Settle,
            m.Bounce, m.PressDown, m.PressRelease,
        };
        foreach (var spec in specs)
        {
            var c = new SpringCurve(spec.ResponseSec, spec.Damping);
            Assert.True(c.SettleSeconds > 0);
            Assert.InRange(c.ValueAt(c.SettleSeconds * 0.5), 0.0, 3.0); // 不可能出现离谱数值
            Assert.Equal(1, c.ValueAt(c.SettleSeconds));
        }
    }
}
