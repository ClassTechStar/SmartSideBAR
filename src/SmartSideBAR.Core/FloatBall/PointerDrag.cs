// 指针/触摸拖拽的采样换算与合并 (P1-1)。
// 借鉴 CyreneCompass 的 physicalDrag.js：
// - 屏幕位移到窗口位移的换算只在边界做一次（乘缩放系数），避免 DPI 反复换算累积误差；
// - 高频采样用"只保留最新一次"的合并队列消费，避免触摸事件堆积导致的抖动与滞后。
namespace SmartSideBAR.Core.FloatBall;

public readonly record struct DragSample(int X, int Y);

public static class PointerDrag
{
    /// <summary>默认拖拽判定阈值（物理像素）。</summary>
    public const int DefaultThreshold = 5;

    /// <summary>
    /// 由"上一次的窗口目标位置 + 两次屏幕采样"算出新的窗口位置。
    /// 换算只用一次 scaleFactor，不做相对累加。
    /// </summary>
    public static DragSample Target(
        DragSample previousTarget, DragSample previousScreen, DragSample currentScreen, double scaleFactor)
    {
        var s = scaleFactor <= 0 ? 1 : scaleFactor;
        return new DragSample(
            (int)Math.Round(previousTarget.X + (currentScreen.X - previousScreen.X) * s),
            (int)Math.Round(previousTarget.Y + (currentScreen.Y - previousScreen.Y) * s));
    }

    /// <summary>位移是否超过阈值（两轴取最大）。</summary>
    public static bool Exceeds(DragSample start, DragSample current, int threshold = DefaultThreshold)
        => Math.Max(Math.Abs(current.X - start.X), Math.Abs(current.Y - start.Y)) > threshold;
}

/// <summary>
/// 最新位移合并队列：并发喂入的样本只保留最后一份，消费者串行处理。
/// 用于把"每个指针移动事件都立刻搬窗口"改成"总是搬向最新位置"。
/// </summary>
public sealed class LatestMoveQueue<T>
{
    private readonly object _gate = new();
    private readonly Func<T, Task> _worker;
    private T? _pending;
    private bool _pendingHasValue;
    private bool _running;

    public LatestMoveQueue(Func<T, Task> worker) => _worker = worker;

    /// <summary>喂入一个样本（覆盖未消费的旧样本）。</summary>
    public void Enqueue(T sample)
    {
        lock (_gate)
        {
            _pending = sample;
            _pendingHasValue = true;
            if (_running) return;
            _running = true;
        }
        _ = DrainAsync();
    }

    private async Task DrainAsync()
    {
        while (true)
        {
            T? next;
            lock (_gate)
            {
                if (!_pendingHasValue)
                {
                    _running = false;
                    return;
                }
                next = _pending;
                _pending = default;
                _pendingHasValue = false;
            }
            try
            {
                if (next is not null) await _worker(next);
            }
            catch
            {
                // 单个样本处理失败不应中断队列
            }
        }
    }
}
