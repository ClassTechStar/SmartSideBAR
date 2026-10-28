// 悬浮球位置恢复与夹取 (P0-1)。
using SmartSideBAR.Core.FloatBall;

namespace SmartSideBAR.Core.Tests;

public class FloatBallPositionPolicyTests
{
    private static readonly WorkAreaRect Area = new(0, 0, 1920, 1080);
    private static readonly WorkAreaRect Primary = new(0, 0, 1920, 1080);

    [Fact]
    public void 位置完整落在工作区内视为可用()
    {
        Assert.True(FloatBallPositionPolicy.Fits(100, 100, 56, 56, Area));
        Assert.False(FloatBallPositionPolicy.Fits(1900, 100, 56, 56, Area)); // 右侧越界
        Assert.False(FloatBallPositionPolicy.Fits(-10, 100, 56, 56, Area));  // 左侧越界
    }

    [Fact]
    public void 位置可用时原样返回且不重置()
    {
        var r = FloatBallPositionPolicy.Restore(100, 200, 56, 56, Area, Primary);
        Assert.Equal(100, r.X);
        Assert.Equal(200, r.Y);
        Assert.False(r.Reset);
    }

    [Fact]
    public void 越界时回到主屏中央并标记重置()
    {
        var r = FloatBallPositionPolicy.Restore(5000, 5000, 56, 56, Area, Primary);
        Assert.True(r.Reset);
        // 居中: (1920-56)/2 = 932, (1080-56)/2 = 512
        Assert.Equal(932, r.X);
        Assert.Equal(512, r.Y);
    }

    [Fact]
    public void 候选工作区为空时按主屏居中()
    {
        var r = FloatBallPositionPolicy.Restore(10, 10, 56, 56, null, Primary);
        Assert.True(r.Reset);
        Assert.Equal(932, r.X);
    }

    [Fact]
    public void 夹取把目标约束进工作区()
    {
        var r = FloatBallPositionPolicy.Clamp(2000, 1200, 56, 56, Area);
        Assert.Equal(1920 - 56, r.X);
        Assert.Equal(1080 - 56, r.Y);
        Assert.False(r.Reset);
    }

    [Fact]
    public void 夹取不会把元素推到工作区外()
    {
        var r = FloatBallPositionPolicy.Clamp(-100, -100, 56, 56, Area);
        Assert.Equal(0, r.X);
        Assert.Equal(0, r.Y);
    }
}
