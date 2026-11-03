// 课表文本解析回归 —— 竞赛演示的关键路径: 评委现场输入课表, 解析失败即演示事故。
using SmartSideBAR.Core.Scheduling;

namespace SmartSideBAR.Core.Tests;

public class LessonPeriodTextTests
{
    [Fact]
    public void Parses_standard_format_with_names()
    {
        var periods = LessonPeriodText.Parse("8:00-8:45 语文; 8:55-9:40 数学");
        Assert.Equal(2, periods.Count);
        Assert.Equal(480, periods[0].StartMinute);
        Assert.Equal(525, periods[0].EndMinute);
        Assert.Equal("语文", periods[0].Name);
        Assert.Equal("数学", periods[1].Name);
    }

    [Fact]
    public void Parses_without_names_and_multiline()
    {
        var periods = LessonPeriodText.Parse("8:00-8:45\n8:55-9:40");
        Assert.Equal(2, periods.Count);
        Assert.Null(periods[0].Name);
    }

    [Fact]
    public void Accepts_minute_numbers_and_fullwidth_semicolon()
    {
        var periods = LessonPeriodText.Parse("480-525 语文；530-570");
        Assert.Equal(2, periods.Count);
        Assert.Equal(480, periods[0].StartMinute);
        Assert.Equal(570, periods[1].EndMinute);
    }

    [Fact]
    public void Skips_invalid_segments_but_keeps_valid_ones()
    {
        var periods = LessonPeriodText.Parse("abc-def; 8:00-8:45; 25:00-26:00; 9:00-8:00");
        Assert.Single(periods);
        Assert.Equal(480, periods[0].StartMinute);
    }

    [Fact]
    public void End_must_be_after_start()
    {
        Assert.Empty(LessonPeriodText.Parse("9:00-8:00"));
    }

    [Fact]
    public void Empty_or_null_returns_empty()
    {
        Assert.Empty(LessonPeriodText.Parse(null));
        Assert.Empty(LessonPeriodText.Parse("   "));
    }

    [Fact]
    public void Format_roundtrips_through_parse()
    {
        var periods = LessonPeriodText.Parse("8:00-8:45 语文; 8:55-9:40");
        var text = LessonPeriodText.Format(periods);
        var again = LessonPeriodText.Parse(text);
        Assert.Equal(periods, again);
    }
}
