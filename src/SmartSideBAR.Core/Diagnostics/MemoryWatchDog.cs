// 内存看门狗。
// 借鉴 ClassIsland 的 MemoryWatchDogService: 常驻程序内存超限时主动重启, 缓解长期运行的泄漏。
// 这里把"判定"抽成纯逻辑(可单测), 重启动作由上层注入。
namespace SmartSideBAR.Core.Diagnostics;

public sealed record MemorySample(long Bytes, DateTimeOffset At);

/// <summary>
/// 连续多次采样超阈值才判定需要重启, 避免一次性抖动误杀。
/// </summary>
public sealed class MemoryWatchDog
{
    private readonly object _gate = new();
    private readonly int _requiredBreaches;
    // 借鉴 SidebarDiagnostics：判定走通用阈值监视器（边沿检测 + 连续命中），
    // 采样留存走有界历史容器（容量即 requiredBreaches），两处逻辑不再各写一份。
    private readonly ThresholdMonitor _monitor;
    private readonly TimeWindowHistory<MemorySample> _history;

    public MemoryWatchDog(long limitBytes, int requiredBreaches = 3)
    {
        LimitBytes = limitBytes;
        _requiredBreaches = Math.Max(1, requiredBreaches);
        _monitor = new ThresholdMonitor(limitBytes, requiredHits: _requiredBreaches);
        _history = new TimeWindowHistory<MemorySample>(_requiredBreaches);
    }

    public long LimitBytes { get; }

    public int RequiredBreaches => _requiredBreaches;

    /// <summary>喂入一次采样, 返回是否应当重启（只在"首次连续超限"那一拍为 true）。</summary>
    public bool Push(MemorySample sample)
    {
        lock (_gate)
        {
            _history.Add(sample, sample.At);
            return _monitor.Push(sample.Bytes, sample.At) == ThresholdTransition.Raised;
        }
    }

    /// <summary>重置(重启后或手动清理后调用)。</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _history.Clear();
            _monitor.Reset();
        }
    }

    public IReadOnlyList<MemorySample> Samples => _history.Snapshot();

    public static long FromMegabytes(int mb) => (long)mb * 1024 * 1024;
}
