// 规则引擎 (P2-2): 条件求值与动作命中。
using SmartSideBAR.Core.Rules;

namespace SmartSideBAR.Core.Tests;

public class RulesetTests
{
    [Fact]
    public void 时间状态条件成立时命中动作()
    {
        var groups = new[]
        {
            new RuleGroup("mute-usb",
            [
                new RuleCondition(RuleConditionKind.LessonState, RuleMatchMode.Equals, "OnClass"),
            ]),
        };
        var ctx = new RuleContext(LessonState: (int)LessonStateForRule.OnClass);
        Assert.Contains("mute-usb", Ruleset.Evaluate(groups, ctx));
    }

    [Fact]
    public void 条件不满足则不命中()
    {
        var groups = new[]
        {
            new RuleGroup("mute-usb",
            [
                new RuleCondition(RuleConditionKind.LessonState, RuleMatchMode.Equals, "OnClass"),
            ]),
        };
        var ctx = new RuleContext(LessonState: (int)LessonStateForRule.Breaking);
        Assert.Empty(Ruleset.Evaluate(groups, ctx));
    }

    [Fact]
    public void 任一匹配模式下单条成立即命中()
    {
        var groups = new[]
        {
            new RuleGroup("collapse",
            [
                new RuleCondition(RuleConditionKind.Fullscreen, RuleMatchMode.Equals, "true"),
                new RuleCondition(RuleConditionKind.HourOfDay, RuleMatchMode.AtLeast, "21"),
            ], AllMustMatch: false),
        };
        Assert.Contains("collapse", Ruleset.Evaluate(groups, new RuleContext(Fullscreen: true, HourOfDay: 9)));
        Assert.Contains("collapse", Ruleset.Evaluate(groups, new RuleContext(Fullscreen: false, HourOfDay: 22)));
        Assert.Empty(Ruleset.Evaluate(groups, new RuleContext(Fullscreen: false, HourOfDay: 10)));
    }

    [Fact]
    public void 无法解析的期望值视为不成立()
    {
        var c = new RuleCondition(RuleConditionKind.HourOfDay, RuleMatchMode.Equals, "不是数字");
        Assert.False(Ruleset.IsSatisfied(c, new RuleContext(HourOfDay: 8)));
    }

    [Fact]
    public void 空条件组不命中()
    {
        var groups = new[] { new RuleGroup("noop", []) };
        Assert.Empty(Ruleset.Evaluate(groups, new RuleContext()));
    }

    [Fact]
    public void 多个命中动作按组顺序去重返回()
    {
        var groups = new[]
        {
            new RuleGroup("a", [new RuleCondition(RuleConditionKind.WeekDay, RuleMatchMode.AtMost, "5")]),
            new RuleGroup("a", [new RuleCondition(RuleConditionKind.WeekDay, RuleMatchMode.AtLeast, "1")]),
            new RuleGroup("b", [new RuleCondition(RuleConditionKind.HourOfDay, RuleMatchMode.AtLeast, "0")]),
        };
        var actions = Ruleset.Evaluate(groups, new RuleContext(WeekDay: 3, HourOfDay: 10));
        Assert.Equal(new[] { "a", "b" }, actions);
    }
}
