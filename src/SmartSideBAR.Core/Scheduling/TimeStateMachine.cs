// 上课/课间时间状态机。
// 借鉴 ClassIsland 的 TimeState 与 LessonsService: 把"到点触发"升级为状态判定,
// 并支持提前 N 秒的预备提醒, 以及不依赖真实时间的调试推进。
namespace SmartSideBAR.Core.Scheduling;

public enum LessonState
{
    /// <summary>无课表或尚未开始</summary>
    Idle,
    /// <summary>预备(距上课 prepareSeconds 内)</summary>
    Prepare,
    /// <summary>上课中</summary>
    OnClass,
    /// <summary>课间</summary>
    Breaking,
    /// <summary>放学</summary>
    AfterSchool,
}

/// <summary>一节课的时间段(自当日 0 点起的分钟数)。</summary>
public sealed record LessonPeriod(int StartMinute, int EndMinute, string? Name = null);

public sealed record LessonStateSnapshot(
    LessonState State,
    LessonPeriod? Current,
    LessonPeriod? Next,
    int RemainingSeconds,
    string? Note);

/// <summary>
/// 纯逻辑时间状态机。时间段按开始时间升序传入; 不依赖系统时钟, 便于单测。
/// </summary>
public sealed class TimeStateMachine
{
    private readonly List<LessonPeriod> _periods;
    private readonly int _prepareSeconds;

    public TimeStateMachine(IEnumerable<LessonPeriod> periods, int prepareSeconds = 60)
    {
        _periods = periods.OrderBy(p => p.StartMinute).ToList();
        _prepareSeconds = Math.Max(0, prepareSeconds);
    }

    public IReadOnlyList<LessonPeriod> Periods => _periods;

    /// <param name="now">当前时刻(自当日 0 点起的秒数)</param>
    public LessonStateSnapshot Evaluate(int nowSeconds)
    {
        if (_periods.Count == 0)
            return new LessonStateSnapshot(LessonState.Idle, null, null, 0, null);

        var nowMin = nowSeconds / 60.0;
        var first = _periods[0];
        var last = _periods[^1];

        if (nowMin < first.StartMinute)
        {
            var wait = (int)Math.Ceiling((first.StartMinute - nowMin) * 60);
            var state = wait <= _prepareSeconds ? LessonState.Prepare : LessonState.Idle;
            return new LessonStateSnapshot(state, null, first, wait, "课前");
        }

        if (nowMin >= last.EndMinute)
            return new LessonStateSnapshot(LessonState.AfterSchool, null, null, 0, "放学");

        for (var i = 0; i < _periods.Count; i++)
        {
            var p = _periods[i];
            if (nowMin >= p.StartMinute && nowMin < p.EndMinute)
            {
                var left = (int)Math.Ceiling((p.EndMinute - nowMin) * 60);
                return new LessonStateSnapshot(LessonState.OnClass, p, NextOf(i), left, p.Name);
            }
            // 课间: 处于本节之后、下一节之前
            var next = NextOf(i);
            if (next is not null && nowMin >= p.EndMinute && nowMin < next.StartMinute)
            {
                var left = (int)Math.Ceiling((next.StartMinute - nowMin) * 60);
                var state = left <= _prepareSeconds ? LessonState.Prepare : LessonState.Breaking;
                return new LessonStateSnapshot(state, null, next, left, "课间");
            }
        }

        return new LessonStateSnapshot(LessonState.AfterSchool, null, null, 0, null);
    }

    private LessonPeriod? NextOf(int index)
        => index + 1 < _periods.Count ? _periods[index + 1] : null;

    /// <summary>状态是否发生了变化(供上层只在变化时弹提醒)。</summary>
    public static bool Changed(LessonStateSnapshot? prev, LessonStateSnapshot next)
        => prev is null || prev.State != next.State || prev.Current != next.Current;
}
