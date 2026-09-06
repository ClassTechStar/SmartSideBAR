// shared/floatball-layout.ts 的逐函数移植 —— 纯算法, 无平台依赖 (附录 A floatball:layout 落点)。
namespace SmartSideBAR.Core.FloatBall;

public readonly record struct RectLike(int X, int Y, int Width, int Height);

public static class FloatBallLayout
{
    public const double FanStartDeg = 4;
    public const double FanSweepDeg = 92;
    public const int FanItemSize = 46;
    public const int FanItemGap = 12;
    public const int FanMargin = 12;
    public const int FanRadiusMin = 96;
    public const int FanRadiusMax = 260;

    public static (int X, int Y) ClampToArea((int X, int Y) pos, (int W, int H) size, RectLike area)
    {
        var maxX = area.X + area.Width - size.W;
        var maxY = area.Y + area.Height - size.H;
        return (
            Math.Min(Math.Max(pos.X, area.X), Math.Max(area.X, maxX)),
            Math.Min(Math.Max(pos.Y, area.Y), Math.Max(area.Y, maxY)));
    }

    public static (int X, int Y, string? Edge) SnapToEdges(
        (int X, int Y) pos, (int W, int H) size, RectLike area, int threshold)
    {
        var p = ClampToArea(pos, size, area);
        if (threshold <= 0) return (p.X, p.Y, null);
        var dLeft = p.X - area.X;
        var dRight = area.X + area.Width - (p.X + size.W);
        var dTop = p.Y - area.Y;
        var dBottom = area.Y + area.Height - (p.Y + size.H);
        var nearest = Math.Min(Math.Min(dLeft, dRight), Math.Min(dTop, dBottom));
        if (nearest > threshold) return (p.X, p.Y, null);
        if (nearest == dLeft) return (area.X, p.Y, "left");
        if (nearest == dRight) return (area.X + area.Width - size.W, p.Y, "right");
        if (nearest == dTop) return (p.X, area.Y, "top");
        return (p.X, area.Y + area.Height - size.H, "bottom");
    }

    public static (int X, int Y) DefaultBallPosition(RectLike area, int ballDip, string side)
    {
        var inset = (int)Math.Round(ballDip * 0.28);
        var x = side == "right" ? area.X + area.Width - ballDip - inset : area.X + inset;
        var y = area.Y + (int)Math.Round(area.Height * 0.62);
        return ClampToArea((x, y), (ballDip, ballDip), area);
    }

    /// <summary>cfg.x/y 为相对 workArea 的偏移; &lt;0 表示未设置 → 默认位置</summary>
    public static (int X, int Y) ResolveBallPosition((int X, int Y) cfg, RectLike area, int ballDip, string side) =>
        cfg.X < 0 || cfg.Y < 0
            ? DefaultBallPosition(area, ballDip, side)
            : ClampToArea((area.X + cfg.X, area.Y + cfg.Y), (ballDip, ballDip), area);

    public static (int X, int Y) ToRelativePosition((int X, int Y) pos, RectLike area) =>
        (Math.Max(0, (int)Math.Round((double)pos.X - area.X)), Math.Max(0, (int)Math.Round((double)pos.Y - area.Y)));

    public static int FanRadius(int count, int itemSize = FanItemSize, int gap = FanItemGap, double sweepDeg = FanSweepDeg)
    {
        if (count <= 1) return FanRadiusMin;
        var step = sweepDeg * Math.PI / 180 / (count - 1);
        var needed = (itemSize + gap) / step;
        return (int)Math.Round(Math.Min(FanRadiusMax, Math.Max(FanRadiusMin, needed)));
    }

    public static (double X, double Y) FanItemOffset(
        int index, int count, int radius, (double X, double Y) dir,
        double startDeg = FanStartDeg, double sweepDeg = FanSweepDeg)
    {
        var startRad = startDeg * Math.PI / 180;
        var sweepRad = sweepDeg * Math.PI / 180;
        var angle = count <= 1 ? startRad + sweepRad / 2 : startRad + sweepRad / (count - 1) * index;
        return (dir.X * radius * Math.Cos(angle), dir.Y * radius * Math.Sin(angle));
    }
}

public readonly record struct FanLayout(bool Expanded, int BallSize, (int X, int Y) BallOffset,
    (double X, double Y) Dir, int Radius, int ItemSize);

public static class FanLayoutComputer
{
    public static FanLayout Collapsed(int ballSize, int itemSize = FloatBallLayout.FanItemSize) =>
        new(false, ballSize, (0, 0), (1, 1), 0, itemSize);

    public static (FanLayout Layout, RectLike Window) ComputeFanLayout(
        (int X, int Y) ball, int ballSize, RectLike area, int count, double uiScale,
        int itemSize = FloatBallLayout.FanItemSize, int gap = FloatBallLayout.FanItemGap)
    {
        var scale = uiScale > 0 ? uiScale : 1;
        var ballDip = (int)Math.Round(ballSize * scale);
        var radius = FloatBallLayout.FanRadius(count, itemSize, gap);
        var roomLeft = ball.X - area.X;
        var roomRight = area.X + area.Width - (ball.X + ballDip);
        var roomTop = ball.Y - area.Y;
        var roomBottom = area.Y + area.Height - (ball.Y + ballDip);
        var dir = (roomRight >= roomLeft ? 1d : -1d, roomBottom >= roomTop ? 1d : -1d);

        var half = ballSize / 2;
        var itemHalf = itemSize / 2;
        double minX = -half, maxX = half, minY = -half, maxY = half;
        for (var i = 0; i < count; i++)
        {
            var o = FloatBallLayout.FanItemOffset(i, count, radius, dir);
            minX = Math.Min(minX, o.X - itemHalf);
            maxX = Math.Max(maxX, o.X + itemHalf);
            minY = Math.Min(minY, o.Y - itemHalf);
            maxY = Math.Max(maxY, o.Y + itemHalf);
        }
        var panelW = (int)Math.Ceiling(maxX - minX + FloatBallLayout.FanMargin * 2);
        var panelH = (int)Math.Ceiling(maxY - minY + FloatBallLayout.FanMargin * 2);
        var centerX = (int)Math.Round(-minX + FloatBallLayout.FanMargin);
        var centerY = (int)Math.Round(-minY + FloatBallLayout.FanMargin);
        var ballOffset = (centerX - half, centerY - half);

        var winW = (int)Math.Round(panelW * scale);
        var winH = (int)Math.Round(panelH * scale);
        var desired = (ball.X - (int)Math.Round(ballOffset.Item1 * scale),
                       ball.Y - (int)Math.Round(ballOffset.Item2 * scale));
        var placed = FloatBallLayout.ClampToArea(desired, (winW, winH), area);
        var compensatedX = ballOffset.Item1 + (int)Math.Round((double)(desired.Item1 - placed.X) / scale);
        var compensatedY = ballOffset.Item2 + (int)Math.Round((double)(desired.Item2 - placed.Y) / scale);
        var safeOffset = (
            Math.Min(Math.Max(compensatedX, 0), Math.Max(0, panelW - ballSize)),
            Math.Min(Math.Max(compensatedY, 0), Math.Max(0, panelH - ballSize)));

        return (new FanLayout(true, ballSize, safeOffset, dir, radius, itemSize),
                new RectLike(placed.X, placed.Y, winW, winH));
    }
}
