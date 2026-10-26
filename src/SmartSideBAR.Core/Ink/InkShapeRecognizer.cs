// 墨迹识别转形 (Ink-to-shape), 纯几何识别器。
// ICC (InkCanvasForClass) 用 WinRT InkAnalyzer (依赖 WPF InkPresenter, 不可移植);
// 这里复刻其意图: 收笔后把手绘折线替换为干净形状。算法 = 主轴分析(PCA) + 残差校验:
//   直线    开放笔画, 最大弦偏差 ≤ 12% 弦长
//   圆/椭圆 封闭 + 主轴系归一半径 r≈1 且方差小; 长短轴比决定圆还是椭圆
//   矩形    封闭 + 四象限角点候选 + 多边形边残差
//   三角形  封闭 + 三角角点候选 + 边残差
// 输出为干净形状的闭合折线点列, 交给 InkCanvas 以 Shape 笔画渲染 (支持任意旋转)。
namespace SmartSideBAR.Core.Ink;

public enum RecognizedShape { None, Line, Circle, Ellipse, Rectangle, Triangle }

public sealed record InkPoint(double X, double Y);

public sealed record ShapeResult(RecognizedShape Kind, IReadOnlyList<InkPoint> Points);

public static class InkShapeRecognizer
{
    private const int MinPoints = 6;

    /// <summary>识别手绘折线。识别失败返回 Kind=None。容差按手势尺寸缩放, 不依赖 DPI。</summary>
    public static ShapeResult Recognize(IReadOnlyList<InkPoint> pts)
    {
        if (pts is null || pts.Count < MinPoints) return new ShapeResult(RecognizedShape.None, []);
        var perimeter = Perimeter(pts);
        if (perimeter <= 0) return new ShapeResult(RecognizedShape.None, []);

        var diag = Diagonal(pts);
        if (diag < 24) return new ShapeResult(RecognizedShape.None, []); // 太小, 误触居多

        var closed = Dist(pts[0], pts[^1]) < perimeter * 0.22;

        // ---- 直线 (开放): 弦长显著 + 最大垂距小
        if (!closed)
        {
            var chord = Dist(pts[0], pts[^1]);
            if (chord > perimeter * 0.55)
            {
                var maxDev = MaxLineDeviation(pts);
                if (maxDev <= Math.Max(6, chord * 0.12))
                {
                    return new ShapeResult(RecognizedShape.Line, [pts[0], pts[^1]]);
                }
            }
            return new ShapeResult(RecognizedShape.None, []);
        }

        // ---- 封闭形状: 主轴系 (PCA) 归一后分类
        var (cx, cy, theta) = PrincipalAxes(pts);
        var (cos, sin) = (Math.Cos(theta), Math.Sin(theta));
        var rotated = pts.Select(p =>
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            return new InkPoint(dx * cos + dy * sin, -dx * sin + dy * cos);
        }).ToList();

        var halfW = (rotated.Max(p => p.X) - rotated.Min(p => p.X)) / 2;
        var halfH = (rotated.Max(p => p.Y) - rotated.Min(p => p.Y)) / 2;
        if (halfW < 8 || halfH < 8) return new ShapeResult(RecognizedShape.None, []);

        // 归一到单位圆域: 椭圆边界上的点 r≈1
        var radii = rotated.Select(p => Math.Sqrt(Math.Pow(p.X / halfW, 2) + Math.Pow(p.Y / halfH, 2))).ToList();
        var meanR = radii.Average();
        var stdR = Math.Sqrt(radii.Select(r => Math.Pow(r - meanR, 2)).Average());
        var axisRatio = Math.Min(halfW, halfH) / Math.Max(halfW, halfH);

        var candidates = new List<(RecognizedShape Kind, double Residual, Func<List<InkPoint>> Build)>();

        if (meanR is >= 0.72 and <= 1.28 && stdR <= 0.2)
        {
            candidates.Add((axisRatio >= 0.82 ? RecognizedShape.Circle : RecognizedShape.Ellipse,
                stdR, () => BuildEllipse(cx, cy, halfW, halfH, theta)));
        }

        var rect = TryQuadPolygon(pts, rotated, cx, cy, cos, sin);
        if (rect is { } r) candidates.Add((RecognizedShape.Rectangle, r.Residual, () => BuildPoly(r.Corners!, cx, cy, cos, sin)));

        var tri = TryTriangle(rotated);
        if (tri is { } t) candidates.Add((RecognizedShape.Triangle, t.Residual, () => BuildPoly(t.Corners!, cx, cy, cos, sin)));

        if (candidates.Count == 0) return new ShapeResult(RecognizedShape.None, []);
        var best = candidates.OrderBy(c => c.Residual).First();
        return new ShapeResult(best.Kind, best.Build());
    }

    // ---- 多边形 (矩形/三角形) 角点候选 + 残差 ----

    /// <summary>矩形: 四象限各取「角向幅值 |u|+|v| 最大」的点作为角点候选。</summary>
    private static (double Residual, List<InkPoint>? Corners)? TryQuadPolygon(
        IReadOnlyList<InkPoint> pts, List<InkPoint> rotated,
        double cx, double cy, double cos, double sin)
    {
        var corner = new InkPoint?[4];
        var score = new double[4];
        foreach (var p in rotated)
        {
            var qx = p.X >= 0 ? 1 : 0;
            var qy = p.Y >= 0 ? 1 : 0;
            var q = qy * 2 + qx;
            var mag = Math.Abs(p.X) + Math.Abs(p.Y);
            if (mag > score[q])
            {
                score[q] = mag;
                corner[q] = p;
            }
        }
        if (corner.Any(c => c is null)) return null; // 某象限没点 → 角点数不足

        var rotatedCorners = corner.Cast<InkPoint>()
            .OrderBy(p => Math.Atan2(p.Y, p.X))
            .ToList();

        // 共线角淘汰: 三角形的"第四象限角点"常恰好落在某条边上 (内角≈180°),
        // 不否决的话矩形会以更小残差抢走三角形
        for (var i = 0; i < 4; i++)
        {
            var prev = rotatedCorners[(i + 3) % 4];
            var cur = rotatedCorners[i];
            var next = rotatedCorners[(i + 1) % 4];
            var a1 = Math.Atan2(cur.Y - prev.Y, cur.X - prev.X);
            var a2 = Math.Atan2(next.Y - cur.Y, next.X - cur.X);
            var turn = Math.Abs(NormalizeAngle(a2 - a1));
            if (turn < Math.PI / 6) return null; // 转角 < 30° → 近共线
        }

        var size = Math.Min(
            rotated.Max(p => p.X) - rotated.Min(p => p.X),
            rotated.Max(p => p.Y) - rotated.Min(p => p.Y));
        if (size <= 0) return null;

        var residual = PolygonResidual(rotated, rotatedCorners);
        var tolerance = Math.Max(7, size * 0.16);
        if (residual > tolerance) return null;
        return (residual / size, rotatedCorners);
    }

    /// <summary>三角形: 质心最远点起步的贪心三点选取 (三角形在主轴系下常只覆盖三个象限,
    /// 四象限法会因空象限失效)。随后按角向排序并做边残差校验。</summary>
    private static (double Residual, List<InkPoint>? Corners)? TryTriangle(List<InkPoint> rotated)
    {
        var centroid = new InkPoint(rotated.Average(p => p.X), rotated.Average(p => p.Y));
        var v1 = rotated.OrderByDescending(p => Dist(p, centroid)).First();
        var v2 = rotated.OrderByDescending(p => Dist(p, v1)).First();
        var v3 = rotated
            .Select(p => (p, d: Math.Min(Dist(p, v1), Dist(p, v2))))
            .OrderByDescending(x => x.d)
            .First().p;

        // 三个候选彼此要有足够间距 (否则是圆弧被贪心拆出的伪角点)
        var size = Math.Min(
            rotated.Max(p => p.X) - rotated.Min(p => p.X),
            rotated.Max(p => p.Y) - rotated.Min(p => p.Y));
        if (size <= 0) return null;
        if (Dist(v2, v3) < size * 0.4 || Dist(v1, v3) < size * 0.4) return null;

        var corners = new List<InkPoint> { v1, v2, v3 }
            .OrderBy(p => Math.Atan2(p.Y - centroid.Y, p.X - centroid.X))
            .ToList();

        var residual = PolygonResidual(rotated, corners);
        var tolerance = Math.Max(7, size * 0.16);
        if (residual > tolerance) return null;
        return (residual / size, corners);
    }

    /// <summary>每个采样点到多边形边的最大距离 (残差)。</summary>
    private static double PolygonResidual(List<InkPoint> pts, List<InkPoint> corners)
    {
        var poly = corners.Concat([corners[0]]).ToList();
        var maxResidual = 0.0;
        foreach (var p in pts)
        {
            var minEdge = double.MaxValue;
            for (var i = 0; i < poly.Count - 1; i++)
            {
                minEdge = Math.Min(minEdge, SegmentDistance(p, poly[i], poly[i + 1]));
            }
            maxResidual = Math.Max(maxResidual, minEdge);
        }
        return maxResidual;
    }

    private static List<InkPoint> BuildPoly(List<InkPoint> rotatedCorners, double cx, double cy, double cos, double sin)
    {
        var result = new List<InkPoint>();
        foreach (var p in rotatedCorners)
        {
            // 逆旋转回原坐标系 (含平移)
            result.Add(new InkPoint(cx + p.X * cos - p.Y * sin, cy + p.X * sin + p.Y * cos));
        }
        result.Add(result[0]); // 闭合
        return result;
    }

    private static List<InkPoint> BuildEllipse(double cx, double cy, double halfW, double halfH, double theta)
    {
        var (cos, sin) = (Math.Cos(theta), Math.Sin(theta));
        var result = new List<InkPoint>(73);
        for (var i = 0; i <= 72; i++)
        {
            var t = i * Math.PI / 36;
            var ex = halfW * Math.Cos(t);
            var ey = halfH * Math.Sin(t);
            result.Add(new InkPoint(cx + ex * cos - ey * sin, cy + ex * sin + ey * cos));
        }
        return result;
    }

    // ---- 几何工具 ----

    /// <summary>主轴方向 (协方差矩阵主特征向量角度)。</summary>
    private static (double Cx, double Cy, double Theta) PrincipalAxes(IReadOnlyList<InkPoint> pts)
    {
        var cx = pts.Average(p => p.X);
        var cy = pts.Average(p => p.Y);
        double sxx = 0, syy = 0, sxy = 0;
        foreach (var p in pts)
        {
            sxx += (p.X - cx) * (p.X - cx);
            syy += (p.Y - cy) * (p.Y - cy);
            sxy += (p.X - cx) * (p.Y - cy);
        }
        var theta = 0.5 * Math.Atan2(2 * sxy, sxx - syy);
        return (cx, cy, theta);
    }

    private static double MaxLineDeviation(IReadOnlyList<InkPoint> pts)
    {
        var a = pts[0];
        var b = pts[^1];
        var len = Dist(a, b);
        if (len < 1e-6) return double.MaxValue;
        var max = 0.0;
        foreach (var p in pts)
        {
            var cross = Math.Abs((b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X));
            max = Math.Max(max, cross / len);
        }
        return max;
    }

    private static double SegmentDistance(InkPoint p, InkPoint a, InkPoint b)
    {
        var abx = b.X - a.X;
        var aby = b.Y - a.Y;
        var lenSq = abx * abx + aby * aby;
        if (lenSq < 1e-9) return Dist(p, a);
        var t = Math.Clamp(((p.X - a.X) * abx + (p.Y - a.Y) * aby) / lenSq, 0, 1);
        return Dist(p, new InkPoint(a.X + t * abx, a.Y + t * aby));
    }

    private static double Perimeter(IReadOnlyList<InkPoint> pts)
    {
        var sum = 0.0;
        for (var i = 1; i < pts.Count; i++) sum += Dist(pts[i - 1], pts[i]);
        return sum;
    }

    private static double Diagonal(IReadOnlyList<InkPoint> pts)
    {
        var minX = pts.Min(p => p.X);
        var maxX = pts.Max(p => p.X);
        var minY = pts.Min(p => p.Y);
        var maxY = pts.Max(p => p.Y);
        return Math.Sqrt(Math.Pow(maxX - minX, 2) + Math.Pow(maxY - minY, 2));
    }

    private static double Dist(InkPoint a, InkPoint b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    /// <summary>归一化到 (-π, π]。</summary>
    private static double NormalizeAngle(double a)
    {
        while (a > Math.PI) a -= 2 * Math.PI;
        while (a <= -Math.PI) a += 2 * Math.PI;
        return a;
    }
}
