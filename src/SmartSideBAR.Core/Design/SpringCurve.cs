// 物理弹簧曲线（2026-11）。
// 替代此前用 BackEaseOut 近似回弹的做法：直接解二阶阻尼振子方程
//     x'' + 2ζω₀x' + ω₀²x = 0,  x(0)=0, x'(0)=0
// 并对阶跃输入做归一化，得到 0→1 的位置曲线。
//
// 参数与 mochi-compose 的 MochiMotionTokens 同源：
//     ω₀ = 2π / response      （response 单位秒，决定"多快到达"）
//     ζ  = damping            （阻尼比：1=临界阻尼不过冲，<1 有过冲回弹）
//
// 只放数学，不依赖 Avalonia；Avalonia 的 IEasing 包装在 M3.cs。
namespace SmartSideBAR.Core.Design;

public sealed class SpringCurve
{
    /// <summary>收敛判据：包络衰减到 0.1% 视为到位。</summary>
    public const double SettleEpsilon = 0.001;

    public SpringCurve(double responseSec, double dampingRatio)
    {
        if (responseSec <= 0) throw new ArgumentOutOfRangeException(nameof(responseSec));
        if (dampingRatio <= 0) throw new ArgumentOutOfRangeException(nameof(dampingRatio));

        ResponseSec = responseSec;
        Zeta = dampingRatio;
        Omega = 2 * Math.PI / responseSec;
    }

    public double ResponseSec { get; }

    /// <summary>阻尼比 ζ。</summary>
    public double Zeta { get; }

    /// <summary>固有角频率 ω₀（rad/s）。</summary>
    public double Omega { get; }

    /// <summary>
    /// 包络衰减到 0.1% 所需的秒数（也是动画应取的时长）。
    /// 衰减率取"最慢的那个模态"：欠阻尼/临界用 ζω₀；过阻尼用慢根
    /// |r1| = ω₀(ζ - √(ζ²-1))——直接用 ζω₀ 会低估过阻尼的收敛时间。
    /// </summary>
    public double SettleSeconds
    {
        get
        {
            var decay = Zeta <= 1
                ? Zeta
                : Zeta - Math.Sqrt(Zeta * Zeta - 1);
            return Math.Log(1 / SettleEpsilon) / (decay * Omega);
        }
    }

    /// <summary>理论最大过冲比例（ζ≥1 时为 0）。</summary>
    public double OverShootPercent =>
        Zeta >= 1 ? 0 : Math.Exp(-Zeta * Math.PI / Math.Sqrt(1 - Zeta * Zeta));

    /// <summary>
    /// t 秒时的归一化位置。t=0 返回 0，t≥SettleSeconds 返回 1。
    /// 欠阻尼会短暂超过 1（回弹），这是物理正确行为，不是 bug。
    /// </summary>
    public double ValueAt(double seconds)
    {
        if (seconds <= 0) return 0;
        if (seconds >= SettleSeconds) return 1;

        var t = seconds;
        var w = Omega;
        var z = Zeta;

        if (Math.Abs(z - 1) < 1e-6)
        {
            // 临界阻尼：x = 1 - e^(-ωt)(1 + ωt)
            return 1 - Math.Exp(-w * t) * (1 + w * t);
        }

        if (z < 1)
        {
            // 欠阻尼：ωd = ω√(1-ζ²)，x = 1 - e^(-ζωt)(cos ωd t + (ζω/ωd) sin ωd t)
            var wd = w * Math.Sqrt(1 - z * z);
            var envelope = Math.Exp(-z * w * t);
            return 1 - envelope * (Math.Cos(wd * t) + (z * w / wd) * Math.Sin(wd * t));
        }

        // 过阻尼：两个实根，x = 1 - (r2 e^(r1 t) - r1 e^(r2 t)) / (r2 - r1)
        var root = Math.Sqrt(z * z - 1);
        var r1 = -w * (z - root);
        var r2 = -w * (z + root);
        return 1 - (r2 * Math.Exp(r1 * t) - r1 * Math.Exp(r2 * t)) / (r2 - r1);
    }
}
