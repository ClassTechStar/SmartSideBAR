// 通知优先级队列。
// 借鉴 ClassIsland 的 PriorityQueue<NotificationRequest, NotificationPriority>:
// 优先级 = (来源序号, 入队序号, 是否强制置顶); 同优先级按入队顺序先进先出。
using System.Diagnostics.CodeAnalysis;

namespace SmartSideBAR.Core.Messaging;

public enum NotificationLevel
{
    Info = 0,
    Success = 1,
    Warn = 2,
    Error = 3,
}

public sealed record NotificationRequest(
    string Id,
    string Source,
    string Message,
    NotificationLevel Level = NotificationLevel.Info,
    bool IsPriorityOverride = false);

/// <summary>
/// 优先级比较器: 级别越高越先出; 同级别按入队序号 FIFO; 强制置顶项插到最前。
/// </summary>
public sealed class NotificationPriorityComparer : IComparer<NotificationRequest>
{
    private readonly Func<string, int> _sourceRank;

    public NotificationPriorityComparer(Func<string, int>? sourceRank = null)
        => _sourceRank = sourceRank ?? (_ => 0);

    public int Compare(NotificationRequest? x, NotificationRequest? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return 1;
        if (y is null) return -1;

        // 强制置顶优先
        if (x.IsPriorityOverride != y.IsPriorityOverride)
            return x.IsPriorityOverride ? -1 : 1;

        // 级别降序
        if (x.Level != y.Level) return y.Level.CompareTo(x.Level);

        // 来源序号升序(越小越优先)
        var rx = _sourceRank(x.Source);
        var ry = _sourceRank(y.Source);
        if (rx != ry) return rx.CompareTo(ry);

        // 入队序号升序, 保证 FIFO
        return string.CompareOrdinal(x.Id, y.Id);
    }
}

/// <summary>
/// 纯逻辑通知队列: 支持入队、按优先级出队、容量上限与淘汰。
/// </summary>
public sealed class NotificationQueue
{
    private readonly object _gate = new();
    private readonly List<NotificationRequest> _items = new();
    private readonly Func<string, int> _sourceRank;
    private long _seq;

    public NotificationQueue(Func<string, int>? sourceRank = null, int capacity = 50)
    {
        _sourceRank = sourceRank ?? (_ => 0);
        Capacity = Math.Max(1, capacity);
    }

    public int Capacity { get; }

    public int Count { get { lock (_gate) return _items.Count; } }

    public NotificationRequest Enqueue(
        string source,
        string message,
        NotificationLevel level = NotificationLevel.Info,
        bool priorityOverride = false)
    {
        var req = new NotificationRequest(
            (++_seq).ToString("D12"), source, message, level, priorityOverride);
        lock (_gate)
        {
            _items.Add(req);
            if (_items.Count > Capacity) EvictOne();
        }
        return req;
    }

    public NotificationRequest? Dequeue()
    {
        lock (_gate)
        {
            if (_items.Count == 0) return null;
            var comparer = new NotificationPriorityComparer(_sourceRank);
            _items.Sort(comparer);
            var top = _items[0];
            _items.RemoveAt(0);
            return top;
        }
    }

    public NotificationRequest? Peek()
    {
        lock (_gate)
        {
            if (_items.Count == 0) return null;
            var comparer = new NotificationPriorityComparer(_sourceRank);
            _items.Sort(comparer);
            return _items[0];
        }
    }

    public IReadOnlyList<NotificationRequest> Snapshot()
    {
        lock (_gate)
        {
            var comparer = new NotificationPriorityComparer(_sourceRank);
            return _items.OrderBy(x => x, comparer).ToArray();
        }
    }

    public void Clear() { lock (_gate) _items.Clear(); }

    /// <summary>淘汰一条: 优先丢级别最低且最早入队的。</summary>
    private void EvictOne()
    {
        var comparer = new NotificationPriorityComparer(_sourceRank);
        _items.Sort(comparer);
        // 排序后末尾即最低优先级 + 最晚入队, 但我们要丢"最低级别且最早", 取级别最小的第一条
        var minLevel = _items.Min(x => x.Level);
        var victim = _items.First(x => x.Level == minLevel);
        _items.Remove(victim);
    }
}
