// SchedulerServiceTests —— FakeTimeProvider 驱动的可测调度 (方案 §6.10)
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Core.Tests.TestDoubles;

namespace SmartSideBAR.Core.Tests;

public sealed class SchedulerServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-sched-tests-" + Guid.NewGuid().ToString("N")[..8]);

    private (SchedulerService Svc, FakeTimeProvider Time, EventBus Bus, List<ReminderDue> Due) Create(
        List<Reminder>? seed = null)
    {
        var userPath = Path.Combine(_dir, "user", "config.json");
        var config = new ConfigService(new ConfigPaths(
            userPath, Path.Combine(_dir, "pd", "policy.json"), null, null));
        config.Load();
        if (seed is { Count: > 0 })
        {
            config.Set("reminders", seed);
        }
        var bus = new EventBus();
        var due = new List<ReminderDue>();
        bus.Subscribe<ReminderDue>(due.Add);
        var time = new FakeTimeProvider();
        return (new SchedulerService(config, bus, time), time, bus, due);
    }

    private static long NowOf(FakeTimeProvider time) => time.GetUtcNow().ToUnixTimeMilliseconds();

    [Fact]
    public void Start_OverduePeriodic_AdvancesWithoutFiring()
    {
        var time0 = new FakeTimeProvider();
        var overdue = NowOf(time0) - 10 * 60_000L;
        var (svc, time, _, due) = Create(
        [
            new Reminder { Id = "p1", Kind = ReminderKind.Interval, At = overdue, RepeatMin = 5, Note = "周期" },
        ]);
        svc.Start();
        Assert.Empty(due); // 方案 §6.10: 启动不补触发
        var scheduled = svc.ListReminders()[0];
        Assert.True(scheduled.At > NowOf(time)); // 已推进到未来槽位
        svc.Stop();
    }

    [Fact]
    public void Start_OnceExpiredOver1h_CleanedSilently()
    {
        var time0 = new FakeTimeProvider();
        var stale = NowOf(time0) - 2 * 3_600_000L;
        var recent = NowOf(time0) - 30 * 60_000L;
        var (svc, time, _, due) = Create(
        [
            new Reminder { Id = "stale", Kind = ReminderKind.Once, At = stale },
            new Reminder { Id = "recent", Kind = ReminderKind.Once, At = recent },
        ]);
        svc.Start();
        time.Advance(TimeSpan.FromSeconds(2));
        // >1h 过期: 清理不触发 (v1.2.0 语义); ≤1h: 首个 tick 补触发一次后移除
        Assert.DoesNotContain("stale", due.Select(d => d.Id));
        Assert.Single(due);
        Assert.Equal("recent", due[0].Id);
        Assert.Empty(svc.ListReminders()); // stale 被清理 + recent 触发即移除
        svc.Stop();
    }

    [Fact]
    public void Once_FiresAndRemoved()
    {
        var (svc, time, _, due) = Create();
        svc.Start();
        svc.AddReminder(ReminderKind.Once, NowOf(time) + 3_000, note: "上课");
        time.Advance(TimeSpan.FromSeconds(4));
        Assert.Single(due);
        Assert.Equal("上课", due[0].Note);
        Assert.Empty(svc.ListReminders());
        svc.Stop();
    }

    [Fact]
    public void Interval_FiresAndAdvances()
    {
        var (svc, time, _, due) = Create();
        svc.Start();
        svc.AddReminder(ReminderKind.Interval, NowOf(time) + 2_000, note: "坐姿", repeatMin: 5);
        time.Advance(TimeSpan.FromSeconds(3));
        Assert.Single(due);
        var r = svc.ListReminders().Single();
        Assert.True(r.At >= NowOf(time)); // 顺延到未来
        time.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(2));
        Assert.Equal(2, due.Count);       // 下个周期再触发
        svc.Stop();
    }

    [Fact]
    public void Hourly_AlignsToNextFullHour()
    {
        var (svc, time, _, due) = Create();
        svc.Start();
        svc.AddReminder(ReminderKind.Hourly, NowOf(time) + 1_000, note: "眼保健操");
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Single(due);
        var next = DateTimeOffset.FromUnixTimeMilliseconds(svc.ListReminders().Single().At).LocalDateTime;
        Assert.Equal(0, next.Minute);
        Assert.Equal(0, next.Second);
        Assert.Equal(0, next.Millisecond);
        svc.Stop();
    }

    [Fact]
    public void Snooze_PostponesFiring()
    {
        var (svc, time, _, due) = Create();
        svc.Start();
        var r = svc.AddReminder(ReminderKind.Once, NowOf(time) + 1_000, note: "贪睡");
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Single(due);                     // 到点触发
        Assert.Empty(svc.ListReminders());      // once 触发即移除 → 贪睡语义作用于触发前
        // 触发前贪睡:
        var (svc2, time2, _, due2) = Create();
        svc2.Start();
        var r2 = svc2.AddReminder(ReminderKind.Once, NowOf(time2) + 1_000, note: "贪睡2");
        Assert.True(svc2.Snooze(r2.Id, 5));
        time2.Advance(TimeSpan.FromSeconds(5));
        Assert.Empty(due2);                     // 原时间点不触发
        time2.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(58));
        Assert.Single(due2);                    // 贪睡到期触发
        svc.Stop();
        svc2.Stop();
    }

    [Fact]
    public void AddReminder_PersistsToConfig()
    {
        var (svc, time, _, _) = Create();
        svc.Start();
        svc.AddReminder(ReminderKind.Once, NowOf(time) + 60_000, note: "落盘验证");
        Assert.Single(svc.ListReminders());
        svc.Stop();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
