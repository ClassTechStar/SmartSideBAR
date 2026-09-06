// FloatBallLayoutTests —— shared/floatball-layout.ts 逐函数移植的行为对齐验证
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.FloatBall;

namespace SmartSideBAR.Core.Tests;

public sealed class FloatBallLayoutTests
{
    [Fact]
    public void FanRadius_Bounds()
    {
        Assert.Equal(96, FloatBallLayout.FanRadius(1));
        Assert.Equal(96, FloatBallLayout.FanRadius(0));
        Assert.InRange(FloatBallLayout.FanRadius(8), 96, 260);
    }

    [Fact]
    public void ClampToArea_ClampsInside()
    {
        var area = new RectLike(0, 0, 1920, 1040);
        var p = FloatBallLayout.ClampToArea((5000, -100), (56, 56), area);
        Assert.Equal(1920 - 56, p.X);
        Assert.Equal(0, p.Y);
    }

    [Fact]
    public void SnapToEdges_NearLeftSnaps()
    {
        var area = new RectLike(0, 0, 1920, 1040);
        var (x, y, edge) = FloatBallLayout.SnapToEdges((5, 500), (56, 56), area, 24);
        Assert.Equal(0, x);
        Assert.Equal("left", edge);
    }

    [Fact]
    public void SnapToEdges_BeyondThreshold_NoSnap()
    {
        var area = new RectLike(0, 0, 1920, 1040);
        var (_, _, edge) = FloatBallLayout.SnapToEdges((500, 500), (56, 56), area, 24);
        Assert.Null(edge);
    }

    [Fact]
    public void SnapToEdges_ZeroThreshold_Disabled()
    {
        var area = new RectLike(0, 0, 1920, 1040);
        var (_, _, edge) = FloatBallLayout.SnapToEdges((1, 1), (56, 56), area, 0);
        Assert.Null(edge);
    }

    [Fact]
    public void ResolveBallPosition_Unset_UsesDefaultNearSide()
    {
        var area = new RectLike(0, 0, 1920, 1040);
        var (x, _) = FloatBallLayout.ResolveBallPosition((-1, -1), area, 56, "right");
        Assert.True(x > 1920 - 56 - 20, $"右侧默认位应贴右缘, 得到 x={x}");
    }

    [Fact]
    public void ResolveBallPosition_Configured_Clamped()
    {
        var area = new RectLike(0, 0, 1920, 1040);
        var (x, _) = FloatBallLayout.ResolveBallPosition((9999, 100), area, 56, "right");
        Assert.Equal(1920 - 56, x);
    }

    [Fact]
    public void Clamp_NormalizesFloatBallConfig()
    {
        var cfg = new FloatBallConfig
        {
            Size = 500,
            IdleOpacity = 5,
            Actions = ["capture", "longshot", "capture", "bogus"],
            DoubleClick = "explode",
        };
        var clamped = FloatBallNormalize.Clamp(cfg);
        Assert.Equal(96, clamped.Size);
        Assert.Equal(1, clamped.IdleOpacity);
        Assert.Equal(["capture", "longshot"], clamped.Actions); // 去重 + 白名单过滤
        Assert.Equal("toggleSidebar", clamped.DoubleClick);     // 非法值回默认
    }

    [Fact]
    public void Clamp_AppearanceAccent()
    {
        Assert.Equal("#2b6ee0", AppearanceNormalize.NormalizeAccent("#2B6EE0"));
        // 3 位缩写按 JS 语义逐位复制: #2b6 → #22bb66
        Assert.Equal("#22bb66", AppearanceNormalize.NormalizeAccent("#2b6"));
        Assert.Equal("#2B6EE0", AppearanceNormalize.NormalizeAccent("not-a-color"));
        Assert.Equal("#2B6EE0", AppearanceNormalize.NormalizeAccent("#zzz"));
        Assert.Equal("#2B6EE0", AppearanceNormalize.NormalizeAccent(null));
    }
}
