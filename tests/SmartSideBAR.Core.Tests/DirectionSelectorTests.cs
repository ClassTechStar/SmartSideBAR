// 扇形方向选择（死区 + 迟滞）(P1-2)。
using SmartSideBAR.Core.FloatBall;

namespace SmartSideBAR.Core.Tests;

public class DirectionSelectorTests
{
    [Fact]
    public void 死区内返回未选中()
    {
        Assert.Equal(DirectionSelector.None, DirectionSelector.Select(5, 5, -1, 42));
        Assert.Equal(DirectionSelector.None, DirectionSelector.Select(0, 0, 2, 42));
    }

    [Theory]
    [InlineData(0, -100, 0)]   // 正上
    [InlineData(100, 0, 2)]    // 正右
    [InlineData(0, 100, 4)]    // 正下
    [InlineData(-100, 0, 6)]   // 正左
    public void 四正方向判定正确(double dx, double dy, int expected)
    {
        Assert.Equal(expected, DirectionSelector.Select(dx, dy, -1, 10));
    }

    [Fact]
    public void 右上与左下判定正确()
    {
        Assert.Equal(1, DirectionSelector.Select(100, -100, -1, 10)); // 右上
        Assert.Equal(5, DirectionSelector.Select(-100, 100, -1, 10)); // 左下
    }

    [Fact]
    public void 迟滞让边界附近的微小移动不改变选择()
    {
        // 当前选中"上"(0)，角度仍在上扇区中心 ±22.5° 内时保持
        var kept = DirectionSelector.Select(-20, -100, 0, 10);
        Assert.Equal(0, kept);
    }

    [Fact]
    public void 明显越过扇区中心后切换()
    {
        // 已经明显偏向右侧（角度远超上扇区中心 22.5°）
        var switched = DirectionSelector.Select(100, -60, 0, 10);
        Assert.NotEqual(0, switched);
    }

    [Fact]
    public void 当前值非法时直接按扇区返回()
    {
        var sector = DirectionSelector.Select(0, -100, 99, 10);
        Assert.Equal(0, sector);
    }

    [Fact]
    public void 角度距离计算正确()
    {
        Assert.Equal(0, DirectionSelector.AngularDistance(10, 10));
        Assert.Equal(10, DirectionSelector.AngularDistance(0, 10));
        Assert.Equal(10, DirectionSelector.AngularDistance(355, 5)); // 跨 0 度
        Assert.Equal(180, DirectionSelector.AngularDistance(0, 180));
    }
}
