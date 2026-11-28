// 系统指标纯计算 (方案 §5.2, W7-8): 采样差分零 WMI。
// CPU: GetSystemTimes 差分; 网速: 接口字节差分; 内存/磁盘: 直接读。
namespace SmartSideBAR.Core.Metrics;

/// <summary>CPU 占用: 两次 GetSystemTimes 采样差分 → 0-100。</summary>
public static class MetricsCalculator
{
    /// <param name="idle1/total1">上次采样; total = kernel + user (kernel 已含 idle)。</param>
    public static double CpuPercent(ulong idle1, ulong total1, ulong idle2, ulong total2)
    {
        var total = total2 - total1;
        var idle = idle2 - idle1;
        if (total == 0) return 0;
        var busy = total > idle ? total - idle : 0;
        return Math.Clamp(busy * 100.0 / total, 0, 100);
    }

    /// <summary>网速 B/s: 字节差分 (允许计数器回绕: 负值按 0 计)。</summary>
    public static long BytesPerSecond(long prevBytes, long nowBytes, double seconds)
    {
        if (seconds <= 0) return 0;
        var delta = nowBytes - prevBytes;
        return delta <= 0 ? 0 : (long)(delta / seconds);
    }

    /// <summary>滑动均值 (5 点窗口语义)。</summary>
    public static double Smooth(IReadOnlyList<double> samples, int windowSize = 5)
    {
        if (samples.Count == 0) return 0;
        var take = Math.Min(windowSize, samples.Count);
        var sum = 0.0;
        for (var i = samples.Count - take; i < samples.Count; i++) sum += samples[i];
        return sum / take;
    }
}
