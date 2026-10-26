// 轻量重复规则与临时调整。
// 借鉴 Class-Widgets-2: 用 icalendar/recurring 表达重复日程、用 overrides 表达临时换课并跨天清理。
// 这里不引入外部依赖, 只实现教学场景够用的子集: 按周重复(可单/双周)与按天重复。
namespace SmartSideBAR.Core.Scheduling;

public enum RecurrenceMode
{
    None = 0,
    Daily = 1,
    Weekly = 2,
    BiWeekly = 3, // 单双周中的"双周"
}

/// <summary>
/// 重复规则。WeekDay 取值 0=周日 … 6=周六(与 DayOfWeek 一致)。
/// </summary>
public sealed record RecurrenceRule(
    RecurrenceMode Mode,
    int Interval = 1,
    int? WeekDay = null,
    int? StartMinute = null);

/// <summary>
/// 临时调整(暂停某天/改到某时刻)。借鉴 Class-Widgets-2 的 overrides。
/// </summary>
public sealed record ScheduleOverride(
    string Id,
    long DateEpochDay,
    bool? Skip = null,
    int? MoveToMinute = null,
    string? Note = null);

public static class Recurrence
{
    private const long SecondsPerDay = 86_400;

    /// <summary>判断某天是否命中规则。</summary>
    public static bool Matches(RecurrenceRule rule, DateTimeOffset day, DateTimeOffset anchor)
    {
        if (rule.Mode == RecurrenceMode.None) return false;
        if (rule.WeekDay.HasValue && (int)day.DayOfWeek != rule.WeekDay.Value) return false;

        var days = (long)Math.Floor((day.Date - anchor.Date).TotalDays);
        if (days < 0) return false;

        return rule.Mode switch
        {
            RecurrenceMode.Daily => days % Math.Max(1, rule.Interval) == 0,
            RecurrenceMode.Weekly => (days / 7) % Math.Max(1, rule.Interval) == 0,
            RecurrenceMode.BiWeekly => (days / 7) % (2 * Math.Max(1, rule.Interval)) == 0,
            _ => false,
        };
    }

    /// <summary>把重复规则展开成 [from, to) 区间内的具体时刻(UTC epoch ms)。</summary>
    public static IReadOnlyList<long> Expand(
        RecurrenceRule rule,
        DateTimeOffset anchor,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var result = new List<long>();
        if (rule.Mode == RecurrenceMode.None) return result;
        if (to <= from) return result;

        var cursor = new DateTimeOffset(from.Date, from.Offset);
        var end = new DateTimeOffset(to.Date, to.Offset);
        var guard = 0;
        while (cursor < end && guard++ < 4000)
        {
            if (Matches(rule, cursor, anchor))
            {
                var at = cursor.AddMinutes(rule.StartMinute ?? 0);
                if (at >= from && at < to) result.Add(at.ToUnixTimeMilliseconds());
            }
            cursor = cursor.AddDays(1);
        }
        return result;
    }

    /// <summary>取某天生效的临时调整(同天多条取最后一条)。</summary>
    public static ScheduleOverride? FindOverride(IEnumerable<ScheduleOverride> overrides, DateTimeOffset day)
    {
        var target = (long)Math.Floor((day.Date - DateTimeOffset.UnixEpoch.Date).TotalDays / 1.0);
        return overrides.LastOrDefault(o => o.DateEpochDay == target);
    }

    /// <summary>跨天清理: 丢弃早于今天的临时调整(借鉴 CW2 的跨天自动清理)。</summary>
    public static IReadOnlyList<ScheduleOverride> Prune(
        IEnumerable<ScheduleOverride> overrides, DateTimeOffset today)
    {
        var todayDay = (long)Math.Floor((today.Date - DateTimeOffset.UnixEpoch.Date).TotalDays / 1.0);
        return overrides.Where(o => o.DateEpochDay >= todayDay).ToArray();
    }

    public static long ToEpochDay(DateTimeOffset day)
        => (long)Math.Floor((day.Date - DateTimeOffset.UnixEpoch.Date).TotalDays / 1.0);

    public static long DayToMs(long epochDay) => epochDay * SecondsPerDay * 1000;
}
