// 时间状态机 (P1-1): 上课/课间/放学判定与预备提醒。
using SmartSideBAR.Core.Scheduling;

namespace SmartSideBAR.Core.Tests;

public class TimeStateMachineTests
{
    // 上午两节课: 8:00-8:45, 9:00-9:45 (以当日 0 点起的分钟表示)
    private static readonly LessonPeriod[] Day =
    [
        new(8 * 60, 8 * 60 + 45, "第一节"),
        new(9 * 60, 9 * 60 + 45, "第二节"),
    ];

    private static int Sec(int hour, int minute, int second = 0)
        => hour * 3600 + minute * 60 + second;

    [Fact]
    public void 无课表时为待机()
    {
        var m = new TimeStateMachine([]);
        Assert.Equal(LessonState.Idle, m.Evaluate(Sec(8, 0)).State);
    }

    [Fact]
    public void 上课中返回当前节与剩余秒数()
    {
        var m = new TimeStateMachine(Day);
        var snap = m.Evaluate(Sec(8, 30));
        Assert.Equal(LessonState.OnClass, snap.State);
        Assert.Equal("第一节", snap.Current!.Name);
        Assert.Equal(15 * 60, snap.RemainingSeconds);
    }

    [Fact]
    public void 课间返回下一节()
    {
        var m = new TimeStateMachine(Day);
        var snap = m.Evaluate(Sec(8, 50));
        Assert.Equal(LessonState.Breaking, snap.State);
        Assert.Equal("第二节", snap.Next!.Name);
    }

    [Fact]
    public void 课前预备窗口内进入预备态()
    {
        // 距第一节上课还有 30s, 预备窗口 60s
        var m = new TimeStateMachine(Day, prepareSeconds: 60);
        Assert.Equal(LessonState.Prepare, m.Evaluate(Sec(7, 59, 30)).State);
        // 距上课还有 5 分钟, 仍在课前但非预备
        Assert.Equal(LessonState.Idle, m.Evaluate(Sec(7, 55)).State);
    }

    [Fact]
    public void 课间临近下节课时进入预备态()
    {
        var m = new TimeStateMachine(Day, prepareSeconds: 60);
        // 8:59:30 距第二节上课 30s
        Assert.Equal(LessonState.Prepare, m.Evaluate(Sec(8, 59, 30)).State);
    }

    [Fact]
    public void 最后一节结束后为放学()
    {
        var m = new TimeStateMachine(Day);
        Assert.Equal(LessonState.AfterSchool, m.Evaluate(Sec(10, 0)).State);
    }

    [Fact]
    public void 状态变化可被检测()
    {
        var m = new TimeStateMachine(Day);
        var a = m.Evaluate(Sec(8, 30));
        var b = m.Evaluate(Sec(8, 50));
        Assert.True(TimeStateMachine.Changed(a, b));
        Assert.True(TimeStateMachine.Changed(null, a));
        Assert.False(TimeStateMachine.Changed(a, a));
    }
}
