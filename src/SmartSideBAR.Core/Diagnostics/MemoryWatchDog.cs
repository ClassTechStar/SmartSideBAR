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
    private readonly List<MemorySample> _samples = new();
    private readonly int _requiredBreaches;

    public MemoryWatchDog(long limitBytes, int requiredBreaches = 3)
    {
        LimitBytes = limitBytes;
        _requiredBreaches = Math.Max(1, requiredBreaches);
    }

    public long LimitBytes { get; }

    public int RequiredBreaches => _requiredBreaches;

    /// <summary>喂入一次采样, 返回是否应当重启。</summary>
    public bool Push(MemorySample sample)
    {
        lock (_gate)
        {
            _samples.Add(sample);
            // 只保留最近 requiredBreaches 次
            while (_samples.Count > _requiredBreaches) _samples.RemoveAt(0);
            if (_samples.Count < _requiredBreaches) return false;
            return _samples.All(s => s.Bytes >= LimitBytes);
        }
    }

    /// <summary>重置(重启后或手动清理后调用)。</summary>
    public void Reset() { lock (_gate) _samples.Clear(); }

    public IReadOnlyList<MemorySample> Samples { get { lock (_gate) return _samples.ToArray(); } }

    public static long FromMegabytes(int mb) => (long)mb * 1024 * 1024;
}
