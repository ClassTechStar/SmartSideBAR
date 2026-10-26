// 重复规则与临时调整 (P2-3)。
using SmartSideBAR.Core.Scheduling;

namespace SmartSideBAR.Core.Tests;

public class RecurrenceTests
{
    private static readonly DateTimeOffset Anchor =
        new(2026, 10, 5, 0, 0, 0, TimeSpan.Zero); // 周一

    [Fact]
    public void 每周一命中()
    {
        var rule = new RecurrenceRule(RecurrenceMode.Weekly, 1, WeekDay: 1);
        var monday = Anchor.AddDays(7);
        var tuesday = Anchor.AddDays(8);
        Assert.True(Recurrence.Matches(rule, monday, Anchor));
        Assert.False(Recurrence.Matches(rule, tuesday, Anchor));
    }

    [Fact]
    public void 双周只在隔周命中()
    {
        var rule = new RecurrenceRule(RecurrenceMode.BiWeekly, 1, WeekDay: 1);
        Assert.False(Recurrence.Matches(rule, Anchor.AddDays(7), Anchor));
        Assert.True(Recurrence.Matches(rule, Anchor.AddDays(14), Anchor));
    }

    [Fact]
    public void 展开区间内的命中时刻()
    {
        var rule = new RecurrenceRule(RecurrenceMode.Weekly, 1, WeekDay: 1, StartMinute: 8 * 60);
        var from = Anchor;
        var to = Anchor.AddDays(22);
        var hits = Recurrence.Expand(rule, Anchor, from, to);

        // 10/5、10/12、10/19、10/26 四个周一, 末次时刻 08:00 早于 to(10/27 00:00)
        Assert.Equal(4, hits.Count);
        var first = DateTimeOffset.FromUnixTimeMilliseconds(hits[0]);
        Assert.Equal(8, first.Hour);
    }

    [Fact]
    public void 临时调整取当天最后一条()
    {
        var day = Anchor.AddDays(7);
        var epochDay = Recurrence.ToEpochDay(day);
        var overrides = new[]
        {
            new ScheduleOverride("a", epochDay, Skip: true),
            new ScheduleOverride("b", epochDay, MoveToMinute: 600),
        };
        var found = Recurrence.FindOverride(overrides, day);
        Assert.Equal("b", found!.Id);
    }

    [Fact]
    public void 跨天清理丢弃过期调整()
    {
        var yesterday = Recurrence.ToEpochDay(Anchor.AddDays(-1));
        var today = Recurrence.ToEpochDay(Anchor);
        var overrides = new[]
        {
            new ScheduleOverride("old", yesterday),
            new ScheduleOverride("new", today),
        };
        var kept = Recurrence.Prune(overrides, Anchor);
        Assert.Single(kept);
        Assert.Equal("new", kept[0].Id);
    }

    [Fact]
    public void 不重复模式不命中()
    {
        var rule = new RecurrenceRule(RecurrenceMode.None);
        Assert.False(Recurrence.Matches(rule, Anchor, Anchor));
        Assert.Empty(Recurrence.Expand(rule, Anchor, Anchor, Anchor.AddDays(30)));
    }
}
