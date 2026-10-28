// 扇形菜单的方向选择（死区 + 迟滞）(P1-2)。
// 借鉴 CyreneCompass 的 compass_direction：
// - 位移小于死区时返回 -1（未选中），避免手指微动就误触发；
// - 已选中方向在扇区中心 ±22.5° 内保持，越过边界才切换，消除边界抖动。
namespace SmartSideBAR.Core.FloatBall;

public static class DirectionSelector
{
    /// <summary>未选中。</summary>
    public const int None = -1;

    /// <summary>扇区数量（8 方向）。</summary>
    public const int SectorCount = 8;

    /// <summary>
    /// 依据相对原点的位移判定扇区。
    /// 方向编号：0=上，1=右上，2=右，3=右下，4=下，5=左下，6=左，7=左上（顺时针）。
    /// </summary>
    /// <param name="dx">相对原点的横向位移</param>
    /// <param name="dy">相对原点的纵向位移（屏幕坐标，向下为正）</param>
    /// <param name="current">当前已选方向，None 表示未选</param>
    /// <param name="deadZonePx">死区半径</param>
    public static int Select(double dx, double dy, int current, double deadZonePx)
    {
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < deadZonePx) return None;

        // 屏幕 y 向下：+90° 后 0° 指向正上，顺时针递增
        var angle = ((Math.Atan2(dy, dx) * 180.0 / Math.PI) + 90.0).Mod(360.0);
        var sector = (int)Math.Floor((angle + 22.5) / 45.0) % SectorCount;
        if (sector < 0) sector += SectorCount;

        if (current < 0 || current >= SectorCount) return sector;

        // 迟滞：仍落在当前扇区中心的 ±22.5° 内则保持
        var currentCenter = current * 45.0;
        return AngularDistance(angle, currentCenter) <= 22.5 ? current : sector;
    }

    /// <summary>两个角度之间的最小夹角（0–180）。</summary>
    public static double AngularDistance(double a, double b)
    {
        var d = (a - b).Mod(360.0);
        return d > 180.0 ? 360.0 - d : d;
    }
}

internal static class MathExtensions
{
    public static double Mod(this double value, double m)
    {
        var r = value % m;
        return r < 0 ? r + m : r;
    }
}
