// 扇形弧段选择（死区 + 迟滞）(P1-2 接线)。
using SmartSideBAR.Core.FloatBall;

namespace SmartSideBar.Core.Tests;

public class FanArcSelectorTests
{
    // 与 FloatBallLayout 的实际弧一致: 起始 4°, 张角 92°
    private const double Start = 4;
    private const double Sweep = 92;

    [Fact]
    public void 死区内未选中()
    {
        var idx = FanArcSelector.Select(5, 5, 3, Start, Sweep, FanArcSelector.None, 24);
        Assert.Equal(FanArcSelector.None, idx);
    }

    [Fact]
    public void 弧外的角度未选中()
    {
        // 朝正下方：相对起始角约 86° 之外（向下 90° → relative 86°），仍在弧内；
        // 朝左（180°）则明显在弧外
        var idx = FanArcSelector.Select(-100, 0, 3, Start, Sweep, FanArcSelector.None, 24);
        Assert.Equal(FanArcSelector.None, idx);
    }

    [Fact]
    public void 弧内按段分配索引()
    {
        // 弧 4°~96°，3 项 → 每段约 30.67°
        // 30° 落在第一段(4~34.67)
        Assert.Equal(0, FanArcSelector.Select(100 * Math.Cos(Deg(30)), 100 * Math.Sin(Deg(30)),
            3, Start, Sweep, FanArcSelector.None, 24));
        // 50° 落在第二段
        Assert.Equal(1, FanArcSelector.Select(100 * Math.Cos(Deg(50)), 100 * Math.Sin(Deg(50)),
            3, Start, Sweep, FanArcSelector.None, 24));
        // 90° 落在第三段
        Assert.Equal(2, FanArcSelector.Select(100 * Math.Cos(Deg(90)), 100 * Math.Sin(Deg(90)),
            3, Start, Sweep, FanArcSelector.None, 24));
    }

    [Fact]
    public void 迟滞让段边界附近保持原选择()
    {
        // 当前选中第 0 段，角度略越过第一段边界（约 35°，超出半段+迟滞以内）应保持
        var kept = FanArcSelector.Select(100 * Math.Cos(Deg(35)), 100 * Math.Sin(Deg(35)),
            3, Start, Sweep, current: 0, deadZonePx: 24);
        Assert.Equal(0, kept);
    }

    [Fact]
    public void 明显越过边界后切换()
    {
        var switched = FanArcSelector.Select(100 * Math.Cos(Deg(90)), 100 * Math.Sin(Deg(90)),
            3, Start, Sweep, current: 0, deadZonePx: 24);
        Assert.Equal(2, switched);
    }

    [Fact]
    public void 没有扇项时返回未选中()
    {
        Assert.Equal(FanArcSelector.None, FanArcSelector.Select(100, 0, 0, Start, Sweep, -1, 24));
    }

    [Fact]
    public void 角度归一化与夹角()
    {
        Assert.Equal(10, FanArcSelector.Normalize(370));
        Assert.Equal(350, FanArcSelector.Normalize(-10));
        Assert.Equal(10, FanArcSelector.AngularDistance(355, 5));
        Assert.Equal(180, FanArcSelector.AngularDistance(0, 180));
    }

    private static double Deg(double deg) => deg * Math.PI / 180.0;
}
