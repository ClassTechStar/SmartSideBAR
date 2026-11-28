// 分屏网格纯函数 (方案 §8.2, W9-10): 网格声明 → 工作区槽位矩形。
// 语义参考 FancyZones: 槽位间留 8px gutter, 全部可单测。
namespace SmartSideBAR.Core.Snap;

/// <summary>分屏网格声明: cols × rows 等分 (自定义权重列后续 P3)。</summary>
public sealed record SnapGridSpec(int Cols, int Rows)
{
    public static readonly SnapGridSpec TwoColumns = new(2, 1);
    public static readonly SnapGridSpec ThreeColumns = new(3, 1);
    public static readonly SnapGridSpec Quadrant = new(2, 2);
    public static readonly SnapGridSpec OnePlusTwo = new(3, 2); // 特例槽位 0=左半, 1..2=右半上下
    public static readonly SnapGridSpec ThreeRows = new(1, 3);
    public static readonly SnapGridSpec TwoRows = new(1, 2);
}

/// <summary>槽位矩形 (物理/DIP 均可, 调用方保证单位一致)。</summary>
public sealed record SnapRect(int X, int Y, int W, int H);

public static class SnapGrid
{
    public const int Gutter = 8;

    /// <summary>
    /// 计算 spec 在工作区内的全部槽位 (行列序)。OnePlusTwo 特例: 槽 0 = 左半全高。
    /// </summary>
    public static IReadOnlyList<SnapRect> ComputeRects(SnapGridSpec spec, SnapRect work)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Cols < 1 || spec.Rows < 1) return [];
        var rects = new List<SnapRect>(spec.Cols * spec.Rows);

        if (spec.Equals(SnapGridSpec.OnePlusTwo))
        {
            var half = work.W / 2;
            rects.Add(Inset(new SnapRect(work.X, work.Y, half, work.H)));
            var rightW = work.W - half;
            var rowH = work.H / 2;
            rects.Add(Inset(new SnapRect(work.X + half, work.Y, rightW, rowH)));
            rects.Add(Inset(new SnapRect(work.X + half, work.Y + rowH, rightW, work.H - rowH)));
            return rects;
        }

        var cellW = work.W / spec.Cols;
        var cellH = work.H / spec.Rows;
        for (var r = 0; r < spec.Rows; r++)
        {
            for (var c = 0; c < spec.Cols; c++)
            {
                var x = work.X + c * cellW;
                var y = work.Y + r * cellH;
                var w = c == spec.Cols - 1 ? work.X + work.W - x : cellW;
                var h = r == spec.Rows - 1 ? work.Y + work.H - y : cellH;
                rects.Add(Inset(new SnapRect(x, y, w, h)));
            }
        }
        return rects;
    }

    private static SnapRect Inset(SnapRect r) => new(
        r.X + Gutter, r.Y + Gutter,
        Math.Max(1, r.W - Gutter * 2), Math.Max(1, r.H - Gutter * 2));
}
