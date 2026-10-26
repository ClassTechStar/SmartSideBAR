// 通知中心 (Seelen notifications 卡片语法的数据层)。所有 Toast 落一份历史,
// 供「提醒与通知」面板回看与一键清除。内存环形缓冲, 常驻进程轻量持有 (≤50 条)。
using System.Collections.Generic;

namespace SmartSideBAR.Avalonia.Ui;

public sealed record NotificationEntry(string Id, string Message, string Tone, DateTimeOffset Time);

public sealed class NotificationCenter
{
    public static NotificationCenter Instance { get; } = new();

    private const int Max = 50;

    private readonly List<NotificationEntry> _items = []; // 最新在前
    public IReadOnlyList<NotificationEntry> Items => _items;

    /// <summary>新通知入列（ToastService.Show 去重之后调用）。</summary>
    public event Action? Changed;

    public void Push(string message, string tone)
    {
        _items.Insert(0, new NotificationEntry(Id: Guid.NewGuid().ToString("N")[..8], message, tone, DateTimeOffset.Now));
        if (_items.Count > Max) _items.RemoveAt(_items.Count - 1);
        Changed?.Invoke();
    }

    public void Remove(string id)
    {
        var removed = _items.RemoveAll(n => n.Id == id) > 0;
        if (removed) Changed?.Invoke();
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        Changed?.Invoke();
    }
}
