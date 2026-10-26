// 墨迹识别转形的几何回归: 合成形状 + 受控噪声, 钉住识别语义。
// 容差调紧会让真实手绘识别不出, 调松会把折线误判成形状, 这份测试盯住两条底线。
using SmartSideBAR.Core.Ink;

namespace SmartSideBAR.Core.Tests;

public class InkShapeRecognizerTests
{
    private static List<InkPoint> Circle(double cx, double cy, double r, double noise = 0, int seed = 1)
    {
        var pts = new List<InkPoint>();
        for (var i = 0; i <= 72; i++)
        {
            var t = i * Math.PI / 36;
            var rr = r * (1 + noise * Math.Sin(i * 7.3 + seed));
            pts.Add(new InkPoint(cx + rr * Math.Cos(t), cy + rr * Math.Sin(t)));
        }
        return pts;
    }

    private static List<InkPoint> EllipsePath(double cx, double cy, double rx, double ry)
    {
        var pts = new List<InkPoint>();
        for (var i = 0; i <= 72; i++)
        {
            var t = i * Math.PI / 36;
            pts.Add(new InkPoint(cx + rx * Math.Cos(t), cy + ry * Math.Sin(t)));
        }
        return pts;
    }

    private static List<InkPoint> Rect(double x, double y, double w, double h)
    {
        List<InkPoint> pts = [];
        for (var t = 0.0; t <= 1; t += 0.02) pts.Add(new InkPoint(x + w * t, y));           // 上
        for (var t = 0.0; t <= 1; t += 0.02) pts.Add(new InkPoint(x + w, y + h * t));       // 右
        for (var t = 0.0; t <= 1; t += 0.02) pts.Add(new InkPoint(x + w - w * t, y + h));   // 下
        for (var t = 0.0; t <= 1; t += 0.02) pts.Add(new InkPoint(x, y + h - h * t));       // 左
        pts.Add(pts[0]); // 封闭
        return pts;
    }

    private static List<InkPoint> Triangle(double ax, double ay, double bx, double by, double cx, double cy)
    {
        List<InkPoint> pts = [];
        for (var t = 0.0; t <= 1; t += 0.05) pts.Add(new InkPoint(ax + (bx - ax) * t, ay + (by - ay) * t));
        for (var t = 0.0; t <= 1; t += 0.05) pts.Add(new InkPoint(bx + (cx - bx) * t, by + (cy - by) * t));
        for (var t = 0.0; t <= 1; t += 0.05) pts.Add(new InkPoint(cx + (ax - cx) * t, cy + (ay - cy) * t));
        pts.Add(pts[0]);
        return pts;
    }

    private static List<InkPoint> Line(double ax, double ay, double bx, double by)
    {
        var pts = new List<InkPoint>();
        for (var t = 0.0; t <= 1; t += 0.05) pts.Add(new InkPoint(ax + (bx - ax) * t, ay + (by - ay) * t));
        return pts;
    }

    /// <summary>锯齿折线 (程序员随手画的连线): 不闭合、无直线语义。</summary>
    private static List<InkPoint> Zigzag()
    {
        List<InkPoint> pts = [];
        for (var i = 0; i < 40; i++)
        {
            pts.Add(new InkPoint(i * 8.0, (i % 2 == 0) ? 0 : 60 + (i % 3) * 15));
        }
        return pts;
    }

    [Fact]
    public void Perfect_circle_recognized()
    {
        var result = InkShapeRecognizer.Recognize(Circle(300, 300, 120));
        Assert.Equal(RecognizedShape.Circle, result.Kind);
        Assert.Equal(73, result.Points.Count); // 干净圆 = 73 点闭合折线
    }

    [Fact]
    public void Noisy_circle_still_recognized()
    {
        // 手绘抖动 6% 半径 + 收笔不精确闭合
        var pts = Circle(300, 300, 120, noise: 0.06);
        pts.Add(new InkPoint(pts[^1].X + 8, pts[^1].Y - 5));
        Assert.Equal(RecognizedShape.Circle, InkShapeRecognizer.Recognize(pts).Kind);
    }

    [Fact]
    public void Wide_ellipse_recognized_as_ellipse_not_circle()
    {
        Assert.Equal(RecognizedShape.Ellipse, InkShapeRecognizer.Recognize(EllipsePath(300, 200, 200, 80)).Kind);
    }

    [Fact]
    public void Axis_aligned_rectangle_recognized()
    {
        Assert.Equal(RecognizedShape.Rectangle, InkShapeRecognizer.Recognize(Rect(100, 100, 240, 160)).Kind);
    }

    [Fact]
    public void Rotated_rectangle_recognized()
    {
        // 30° 旋转矩形 (主轴系必须能校正, 这是 PCA 路径存在的原因)
        var pts = new List<InkPoint>();
        var angle = Math.PI / 6;
        foreach (var p in Rect(0, 0, 240, 160))
        {
            pts.Add(new InkPoint(
                400 + (p.X - 120) * Math.Cos(angle) - (p.Y - 80) * Math.Sin(angle),
                300 + (p.X - 120) * Math.Sin(angle) + (p.Y - 80) * Math.Cos(angle)));
        }
        Assert.Equal(RecognizedShape.Rectangle, InkShapeRecognizer.Recognize(pts).Kind);
    }

    [Fact]
    public void Triangle_recognized()
    {
        var result = InkShapeRecognizer.Recognize(Triangle(100, 400, 380, 380, 240, 120));
        Assert.Equal(RecognizedShape.Triangle, result.Kind);
    }

    [Fact]
    public void Straight_line_recognized()
    {
        Assert.Equal(RecognizedShape.Line, InkShapeRecognizer.Recognize(Line(50, 50, 500, 320)).Kind);
    }

    [Fact]
    public void Zigzag_is_not_a_shape()
    {
        Assert.Equal(RecognizedShape.None, InkShapeRecognizer.Recognize(Zigzag()).Kind);
    }

    [Fact]
    public void Too_small_or_too_few_points_are_ignored()
    {
        Assert.Equal(RecognizedShape.None, InkShapeRecognizer.Recognize(
            [new InkPoint(0, 0), new InkPoint(5, 5), new InkPoint(9, 2)]).Kind);
        Assert.Equal(RecognizedShape.None, InkShapeRecognizer.Recognize(Circle(0, 0, 5)).Kind); // 尺寸 < 24 → 误触
    }

    [Fact]
    public void Clean_shapes_are_smaller_than_raw_input()
    {
        // 转形的核心收益: 72 点手绘 → 5 点矩形
        var raw = Rect(0, 0, 200, 100);
        var result = InkShapeRecognizer.Recognize(raw);
        Assert.True(result.Points.Count < raw.Count);
    }
}
