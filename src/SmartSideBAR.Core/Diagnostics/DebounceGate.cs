// 防抖闸门（纯逻辑）。
// 借鉴 SidebarDiagnostics 首次设置向导的 OffsetSlider_ValueChanged（Setup.xaml.cs:66-88）：
// 拖动滑杆时每次变化都取消上一次待执行动作，只在停手 N 毫秒后真正落一次。
// 这里把"该不该执行"抽成可单测的闸门，UI 层只负责定期 TryFire。
namespace SmartSideBAR.Core.Diagnostics;

public sealed class DebounceGate
{
    private readonly int _delayMs;
    private long _lastTriggerMs = long.MinValue / 4;
    private bool _pending;

    public DebounceGate(int delayMs) => _delayMs = Math.Max(0, delayMs);

    public int DelayMs => _delayMs;

    /// <summary>记录一次触发（会重置等待窗口）。</summary>
    public void Trigger(long nowMs)
    {
        _lastTriggerMs = nowMs;
        _pending = true;
    }

    /// <summary>到达静默时长则消费一次待执行动作。</summary>
    public bool TryFire(long nowMs)
    {
        if (!_pending) return false;
        if (nowMs - _lastTriggerMs < _delayMs) return false;
        _pending = false;
        return true;
    }

    /// <summary>是否还有待执行动作。</summary>
    public bool IsPending => _pending;

    public void Reset()
    {
        _pending = false;
        _lastTriggerMs = long.MinValue / 4;
    }
}
