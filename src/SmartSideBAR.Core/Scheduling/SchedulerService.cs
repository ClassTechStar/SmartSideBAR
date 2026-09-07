// Core/Scheduling/SchedulerService.cs —— TimeProvider 可测调度 (方案 §6.10)
// 行为基线 = v1.2.0 scheduler.ts: once 触发即移除 / interval 顺延 / hourly 对齐整点 / 贪睡 /
// C1 变更检测落盘。改进: 启动时过期周期提醒**不补触发**(直接推进到未来槽位) —— 方案明确要求。
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;

namespace SmartSideBAR.Core.Scheduling;

public sealed record ReminderDue(string Id, ReminderKind Kind, string? Note, long DueAtEpochMs);

public sealed class SchedulerService(
    ConfigService config,
    IEventBus bus,
    TimeProvider time,
    ILogger<SchedulerService>? log = null) : IDisposable
{
    private const long OnceExpireGraceMs = 3_600_000; // 与 v1.2.0 一致: 过期 >1h 的 once 启动时清理

    private readonly Lock _gate = new();
    private ITimer? _timer;
    private List<Reminder> _reminders = [];

    public void Start()
    {
        lock (_gate)
        {
            if (_timer is not null) return;
            _reminders = [.. config.Current.Reminders];

            var now = NowMs();
            var changed = false;

            // once 过期 >1h → 清理不触发 (v1.2.0 语义); ≤1h → 保留, 由 tick 补触发一次
            var expired = _reminders
                .Where(r => r.Kind == ReminderKind.Once && r.SnoozedUntil is null && now - r.At > OnceExpireGraceMs)
                .ToList();
            if (expired.Count > 0)
            {
                _reminders.RemoveAll(expired.Contains);
                changed = true;
                log?.LogInformation("[Scheduler] 清理 {Count} 条过期 once 提醒", expired.Count);
            }

            // 方案 §6.10: 过期周期提醒启动时不补触发 —— 直接推进到下一个未来槽位
            foreach (var r in _reminders.ToArray()) // 快照遍历: 循环体内会原位替换元素
            {
                if (r.Kind == ReminderKind.Once || EffectiveAt(r) > now) continue;
                var advanced = r with { At = NextSlotAfter(r, now), SnoozedUntil = null };
                var idx = _reminders.IndexOf(r);
                _reminders[idx] = advanced;
                changed = true;
                log?.LogInformation("[Scheduler] 启动追赶: {Id} 推进到未来槽位(不补触发)", r.Id);
            }

            if (changed) PersistIfChanged();

            _timer = time.CreateTimer(OnTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            log?.LogInformation("[Scheduler] 已启动, {Count} 条提醒", _reminders.Count);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            log?.LogInformation("[Scheduler] 已停止");
        }
    }

    public IReadOnlyList<Reminder> ListReminders() { lock (_gate) return _reminders.ToArray(); }

    public Reminder AddReminder(ReminderKind kind, long atEpochMs, string? note = null, int? repeatMin = null)
    {
        var reminder = new Reminder
        {
            Id = $"r{Guid.NewGuid().ToString("N")[..10]}",
            Kind = kind,
            At = atEpochMs,
            Note = note,
            RepeatMin = kind == ReminderKind.Interval ? Math.Max(1, repeatMin ?? 5) : repeatMin,
        };
        lock (_gate)
        {
            _reminders.Add(reminder);
            PersistIfChanged();
        }
        return reminder;
    }

    public bool RemoveReminder(string id)
    {
        lock (_gate)
        {
            var removed = _reminders.RemoveAll(r => r.Id == id) > 0;
            if (removed) PersistIfChanged();
            return removed;
        }
    }

    public bool Snooze(string id, int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minutes, 1);
        lock (_gate)
        {
            var idx = _reminders.FindIndex(r => r.Id == id);
            if (idx < 0) return false;
            var r = _reminders[idx];
            _reminders[idx] = r with { SnoozedUntil = NowMs() + minutes * 60_000L };
            PersistIfChanged();
            return true;
        }
    }

    private void OnTick(object? state)
    {
        var dueEvents = new List<ReminderDue>();
        lock (_gate)
        {
            var now = NowMs();
            var changed = false;
            var toRemove = new List<string>();

            foreach (var original in _reminders.ToArray())
            {
                var r = original;
                // 贪睡中 — 跳过; 贪睡已过期 — 清除标记
                if (r.SnoozedUntil is { } snoozed)
                {
                    if (now < snoozed) continue;
                    var idx = _reminders.IndexOf(original);
                    r = r with { SnoozedUntil = null };
                    _reminders[idx] = r;
                    changed = true;
                }

                if (EffectiveAt(r) > now) continue;

                dueEvents.Add(new ReminderDue(r.Id, r.Kind, r.Note, r.At));
                switch (r.Kind)
                {
                    case ReminderKind.Once:
                        toRemove.Add(r.Id); // 触发即移除 (v1.2.0: firedOnceIds + auto-remove)
                        break;
                    case ReminderKind.Interval:
                        Replace(r, r with { At = NextSlotAfter(r, now) });
                        changed = true;
                        break;
                    case ReminderKind.Hourly:
                        Replace(r, r with { At = NextFullHour() });
                        changed = true;
                        break;
                }
            }

            if (toRemove.Count > 0)
            {
                _reminders.RemoveAll(r => toRemove.Contains(r.Id));
                changed = true;
                log?.LogInformation("[Scheduler] 自动移除 {Count} 条已触发 once 提醒", toRemove.Count);
            }
            if (changed) PersistIfChanged();
        }

        foreach (var evt in dueEvents) bus.Publish(evt); // 锁外广播

        void Replace(Reminder old, Reminder @new)
        {
            var idx = _reminders.IndexOf(old);
            if (idx >= 0) _reminders[idx] = @new;
        }
    }

    private void PersistIfChanged()
    {
        // C1: 仅 reminders 实际变化时写盘 (落盘走 Set 的白名单+ConfigChanged 广播路径)
        if (config.SetRemindersIfChanged(_reminders))
        {
            log?.LogDebug("[Scheduler] reminders 变更已落盘 ({Count} 条)", _reminders.Count);
        }
    }

    private static long EffectiveAt(Reminder r) => Math.Max(r.At, r.SnoozedUntil ?? long.MinValue);

    private static long NextSlotAfter(Reminder r, long now)
    {
        var step = r.Kind == ReminderKind.Hourly
            ? 3_600_000
            : Math.Max(1, r.RepeatMin ?? 5) * 60_000L;
        var next = r.At;
        while (next <= now) next += step;
        return next;
    }

    /// <summary>下一个本地整点 (v1.2.0: setHours(+1,0,0,0) 本地时间语义)</summary>
    private long NextFullHour()
    {
        var localNow = time.GetLocalNow();
        var nextHour = new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, 0, 0)
            .AddHours(1);
        return new DateTimeOffset(nextHour, localNow.Offset).ToUnixTimeMilliseconds();
    }

    private long NowMs() => time.GetUtcNow().ToUnixTimeMilliseconds();

    public void Dispose() => Stop();
}
