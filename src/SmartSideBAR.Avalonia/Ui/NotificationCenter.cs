// 通知中心 (Seelen notifications 卡片语法的数据层)。所有 Toast 落一份历史,
// 供「提醒与通知」面板回看与一键清除。
// 借鉴 SidebarDiagnostics 的 GraphModel（GraphModel.cs:170-185）：除了容量上限，
// 再加一条时间窗口 —— 超过 7 天的旧通知自动回收，常驻进程不会长期攒着无用历史。
using System.Collections.Generic;
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Avalonia.Ui;

public sealed record NotificationEntry(string Id, string Message, string Tone, DateTimeOffset Time);

public sealed class NotificationCenter
{
    public static NotificationCenter Instance { get; } = new();

    private const int Max = 50;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(7);

    private readonly TimeWindowHistory<NotificationEntry> _history = new(Max, Retention);

    /// <summary>最新在前的历史快照（不再暴露可写内部列表）。</summary>
    public IReadOnlyList<NotificationEntry> Items => _history.SnapshotNewestFirst();

    /// <summary>新通知入列（ToastService.Show 去重之后调用）。</summary>
    public event Action? Changed;

    public void Push(string message, string tone)
    {
        var entry = new NotificationEntry(
            Id: Guid.NewGuid().ToString("N")[..8], message, tone, DateTimeOffset.Now);
        _history.Add(entry, entry.Time);
        Changed?.Invoke();
    }

    /// <summary>按当前时刻主动回收过期项（无常驻写入时也能清理）。</summary>
    public int PruneExpired() => _history.Prune(DateTimeOffset.Now);

    public void Remove(string id)
    {
        // 有界历史不提供按键删除，重建一份等价快照即可（最多 50 条，开销可忽略）
        var kept = _history.Snapshot().Where(n => n.Id != id).ToArray();
        if (kept.Length == _history.Count) return;
        _history.Clear();
        foreach (var item in kept) _history.Add(item, item.Time);
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_history.Count == 0) return;
        _history.Clear();
        Changed?.Invoke();
    }
}
