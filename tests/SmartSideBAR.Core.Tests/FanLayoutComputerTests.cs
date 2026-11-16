// FanLayoutComputer: 悬浮球扇形菜单布局纯算法单测 (展开方向/窗口包络/缩放回退)。
using SmartSideBAR.Core.FloatBall;

namespace SmartSideBAR.Core.Tests;

public sealed class FanLayoutComputerTests
{
    private static readonly RectLike Area = new(0, 0, 1920, 1040);

    [Fact]
    public void 收起态零半径()
    {
        var c = FanLayoutComputer.Collapsed(56);
        Assert.False(c.Expanded);
        Assert.Equal(0, c.Radius);
        Assert.Equal((0, 0), c.BallOffset);
    }

    [Fact]
    public void 展开布局窗口包住球心()
    {
        var (layout, win) = FanLayoutComputer.ComputeFanLayout((900, 500), 56, Area, 5, 1.0);
        Assert.True(layout.Expanded);
        Assert.InRange(layout.Radius, FloatBallLayout.FanRadiusMin, FloatBallLayout.FanRadiusMax);
        Assert.True(win.Width > 0 && win.Height > 0);
        var ballCenterX = win.X + layout.BallOffset.X + 28;
        var ballCenterY = win.Y + layout.BallOffset.Y + 28;
        Assert.InRange(ballCenterX, win.X, win.X + win.Width);
        Assert.InRange(ballCenterY, win.Y, win.Y + win.Height);
    }

    [Fact]
    public void 扇形朝空间更大的一侧展开()
    {
        var (left, _) = FanLayoutComputer.ComputeFanLayout((100, 500), 56, Area, 5, 1.0);
        Assert.Equal(1d, left.Dir.X); // 球贴左缘, 右侧空间大 → 朝右展开
        var (right, _) = FanLayoutComputer.ComputeFanLayout((1800, 500), 56, Area, 5, 1.0);
        Assert.Equal(-1d, right.Dir.X); // 球贴右缘 → 朝左展开
    }

    [Fact]
    public void 扇面项偏移沿扇形方向()
    {
        var (x, y) = FloatBallLayout.FanItemOffset(0, 5, 120, (1, 1));
        Assert.True(x > 0 && y > 0);
        var (x2, y2) = FloatBallLayout.FanItemOffset(0, 1, 120, (-1, 1));
        Assert.True(x2 < 0 && y2 > 0);
    }

    [Fact]
    public void 非法缩放按1回退()
    {
        var (_, win0) = FanLayoutComputer.ComputeFanLayout((900, 500), 56, Area, 5, 0);
        var (_, win1) = FanLayoutComputer.ComputeFanLayout((900, 500), 56, Area, 5, 1);
        Assert.Equal(win1, win0);
    }

    [Fact]
    public void 缩放放大窗口尺寸()
    {
        var (_, win1) = FanLayoutComputer.ComputeFanLayout((900, 500), 56, Area, 5, 1.0);
        var (_, win2) = FanLayoutComputer.ComputeFanLayout((900, 500), 56, Area, 5, 2.0);
        Assert.True(win2.Width > win1.Width, $"2x 缩放窗口应更宽: {win1.Width} → {win2.Width}");
    }
}
