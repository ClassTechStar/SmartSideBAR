// 扇形菜单的弧段选择（死区 + 迟滞）(P1-2 接线)。
// CyreneCompass 的罗盘是"8 方向绕一圈"，而本项目的扇形是一段圆弧
// （FanStartDeg ~ FanStartDeg+FanSweepDeg）。所以这里按"弧段"判定：
// - 位移小于死区 → 未选中；
// - 角度落在弧外 → 未选中；
// - 已选中项在自身弧段中心 ±(半段 + 迟滞) 内保持，越过才切换，消除边界抖动。
namespace SmartSideBAR.Core.FloatBall;

public static class FanArcSelector
{
    /// <summary>未选中。</summary>
    public const int None = -1;

    /// <summary>默认迟滞角度（度）。</summary>
    public const double DefaultHysteresisDeg = 4.0;

    /// <summary>
    /// 按弧段选中一项。
    /// </summary>
    /// <param name="dx">相对扇形原点的横向位移（DIP）</param>
    /// <param name="dy">相对扇形原点的纵向位移（DIP，向下为正）</param>
    /// <param name="count">扇项数量</param>
    /// <param name="startDeg">弧起始角（度，0 = 正右，顺时针为正）</param>
    /// <param name="sweepDeg">弧张角（度）</param>
    /// <param name="current">当前选中索引，None 表示未选</param>
    /// <param name="deadZonePx">死区半径</param>
    /// <param name="hysteresisDeg">迟滞角度</param>
    public static int Select(
        double dx, double dy, int count, double startDeg, double sweepDeg,
        int current, double deadZonePx, double hysteresisDeg = DefaultHysteresisDeg)
    {
        if (count <= 0) return None;

        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < deadZonePx) return None;

        var angle = Normalize(Math.Atan2(dy, dx) * 180.0 / Math.PI);
        var relative = Normalize(angle - startDeg);

        // 落在弧外（含反向）视为未选中
        if (sweepDeg > 0 && relative > sweepDeg) return None;

        var seg = sweepDeg / count;
        var raw = (int)Math.Floor(relative / seg);
        if (raw < 0) raw = 0;
        if (raw >= count) raw = count - 1;

        if (current < 0 || current >= count) return raw;

        // 迟滞：仍落在当前项弧段中心 ±(半段 + 迟滞) 内则保持
        var currentCenter = startDeg + seg * (current + 0.5);
        var half = seg / 2.0 + hysteresisDeg;
        return AngularDistance(angle, Normalize(currentCenter)) <= half ? current : raw;
    }

    /// <summary>角度归一化到 [0,360)。</summary>
    public static double Normalize(double deg)
    {
        var r = deg % 360.0;
        return r < 0 ? r + 360.0 : r;
    }

    /// <summary>两个角度的最小夹角（0–180）。</summary>
    public static double AngularDistance(double a, double b)
    {
        var d = Normalize(a - b);
        return d > 180.0 ? 360.0 - d : d;
    }
}
