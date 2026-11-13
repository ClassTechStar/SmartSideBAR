// 有界时间窗口历史。
// 借鉴 SidebarDiagnostics 的 GraphModel.Metric_PropertyChanged（GraphModel.cs:170-185）：
// 每次追加前先丢掉超出时间窗口的记录，再用容量上限兜底，常驻进程不会无限涨内存。
namespace SmartSideBAR.Core.Diagnostics;

/// <summary>
/// 按"时间窗口 + 容量"双重约束的历史容器。插入顺序保留（旧 → 新）。
/// window 为 null 表示不按时间裁剪，仅用容量约束。
/// </summary>
public sealed class TimeWindowHistory<T>
{
    private readonly object _gate = new();
    private readonly List<(T Item, DateTimeOffset At)> _items = [];
    private readonly TimeSpan? _window;
    private readonly int _capacity;

    public TimeWindowHistory(int capacity, TimeSpan? window = null)
    {
        _capacity = Math.Max(1, capacity);
        _window = window is { } w && w > TimeSpan.Zero ? w : null;
    }

    public int Count { get { lock (_gate) return _items.Count; } }

    /// <summary>追加一条，并按窗口与容量裁剪。</summary>
    public void Add(T item, DateTimeOffset at)
    {
        lock (_gate)
        {
            if (_window is { } w)
            {
                var cutoff = at - w;
                _items.RemoveAll(x => x.At < cutoff);
            }
            _items.Add((item, at));
            while (_items.Count > _capacity) _items.RemoveAt(0);
        }
    }

    /// <summary>按插入顺序（旧 → 新）快照。</summary>
    public IReadOnlyList<T> Snapshot()
    {
        lock (_gate) return _items.Select(x => x.Item).ToArray();
    }

    /// <summary>按新 → 旧快照（通知中心等最新在前的展示用）。</summary>
    public IReadOnlyList<T> SnapshotNewestFirst()
    {
        lock (_gate)
        {
            var result = new List<T>(_items.Count);
            for (var i = _items.Count - 1; i >= 0; i--) result.Add(_items[i].Item);
            return result;
        }
    }

    /// <summary>按当前时刻主动裁剪（无新数据时也能回收过期项）。</summary>
    public int Prune(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_window is not { } w) return 0;
            var cutoff = now - w;
            return _items.RemoveAll(x => x.At < cutoff);
        }
    }

    public void Clear() { lock (_gate) _items.Clear(); }
}
