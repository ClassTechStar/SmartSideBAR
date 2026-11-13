// 阈值告警：边沿检测 + 连续命中 + 迟滞。
// 借鉴 SidebarDiagnostics 的 BaseMetric（Monitoring.cs:1443-1455 / 1602-1655）：
// 「超过阈值才置告警态、回落才清除、状态只在变化时切换」，并把闪烁/变色这类
// 表现层动作留给调用方。这里补上参考项目没有的：连续 N 次命中才触发（防抖动）
// 与回落迟滞带（防阈值附近反复抖）。
namespace SmartSideBAR.Core.Diagnostics;

public enum ThresholdTransition
{
    /// <summary>无状态变化</summary>
    None,
    /// <summary>本次采样使告警态从 false → true</summary>
    Raised,
    /// <summary>本次采样使告警态从 true → false</summary>
    Cleared,
}

/// <summary>
/// 纯逻辑阈值监视器。喂入采样值，返回本次是否发生状态跃迁（不是"当前是否超限"）。
/// </summary>
public sealed class ThresholdMonitor
{
    private readonly object _gate = new();
    private int _consecutiveHits;

    public ThresholdMonitor(double threshold, int requiredHits = 1, double hysteresis = 0)
    {
        Threshold = threshold;
        RequiredHits = Math.Max(1, requiredHits);
        Hysteresis = Math.Max(0, hysteresis);
    }

    public double Threshold { get; }
    public int RequiredHits { get; }
    public double Hysteresis { get; }
    public bool IsRaised { get { lock (_gate) return _isRaised; } }
    private bool _isRaised;

    /// <summary>喂入一次采样。返回状态跃迁；只有跃迁那一拍返回非 None。</summary>
    public ThresholdTransition Push(double value, DateTimeOffset? at = null)
    {
        lock (_gate)
        {
            if (value >= Threshold)
            {
                _consecutiveHits++;
                if (_consecutiveHits >= RequiredHits && !_isRaised)
                {
                    _isRaised = true;
                    return ThresholdTransition.Raised;
                }
                return ThresholdTransition.None;
            }

            // 落在迟滞带内：既不累加也不清除，保持现状
            if (value > Threshold - Hysteresis) return ThresholdTransition.None;

            _consecutiveHits = 0;
            if (_isRaised)
            {
                _isRaised = false;
                return ThresholdTransition.Cleared;
            }
            return ThresholdTransition.None;
        }
    }

    public void Reset()
    {
        lock (_gate)
        {
            _consecutiveHits = 0;
            _isRaised = false;
        }
    }
}
