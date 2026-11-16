// P2-6: AutomationRules 映射/求值与 AutomationEngine 边沿触发的纯逻辑单测。
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Rules;

namespace SmartSideBAR.Core.Tests;

public sealed class AutomationRulesTests
{
    private static RuleContext Ctx(int lesson = 0, bool fullscreen = false, int hour = 8, int weekday = 1) =>
        new(LessonState: lesson, Fullscreen: fullscreen, HourOfDay: hour, WeekDay: weekday);

    private static AutomationRuleConfig Rule(string action, AutomationConditionConfig[] conditions,
        bool enabled = true, bool all = true) =>
        new() { Enabled = enabled, ActionId = action, AllMustMatch = all, Conditions = [.. conditions] };

    private static AutomationConditionConfig Cond(RuleConditionKind kind, RuleMatchMode mode, string value) =>
        new() { Kind = kind, Mode = mode, Value = value };

    [Fact]
    public void 禁用规则与空动作不映射()
    {
        Assert.Null(AutomationRules.ToGroup(Rule("dock", [], enabled: false)));
        Assert.Null(AutomationRules.ToGroup(Rule("   ", [])));
    }

    [Fact]
    public void 映射透传条件与匹配方式()
    {
        var g = AutomationRules.ToGroup(Rule("workspace:上课",
        [
            Cond(RuleConditionKind.LessonState, RuleMatchMode.Equals, "OnClass"),
            Cond(RuleConditionKind.Fullscreen, RuleMatchMode.Equals, "False"),
        ], all: false));
        Assert.NotNull(g);
        Assert.Equal("workspace:上课", g!.ActionId);
        Assert.False(g.AllMustMatch);
        Assert.Equal(2, g.Conditions.Count);
    }

    [Fact]
    public void 批量映射过滤禁用项()
    {
        var groups = AutomationRules.ToGroups(
        [
            Rule("dock", [], enabled: false),
            Rule("ime", []),
            Rule("   ", []),
        ]);
        Assert.Single(groups);
        Assert.Equal("ime", groups[0].ActionId);
    }

    [Fact]
    public void 上课时切工作区场景成立()
    {
        var rules = new[] { Rule("workspace:上课",
            [Cond(RuleConditionKind.LessonState, RuleMatchMode.Equals, "OnClass")]) };
        Assert.Equal(["workspace:上课"], AutomationRules.Evaluate(rules, Ctx(lesson: 2)));
        Assert.Empty(AutomationRules.Evaluate(rules, Ctx(lesson: 0)));
    }

    [Fact]
    public void 任一满足模式触发()
    {
        var rules = new[] { Rule("dock",
        [
            Cond(RuleConditionKind.Fullscreen, RuleMatchMode.Equals, "True"),
            Cond(RuleConditionKind.HourOfDay, RuleMatchMode.AtLeast, "20"),
        ], all: false) };
        Assert.Equal(["dock"], AutomationRules.Evaluate(rules, Ctx(hour: 21)));
        Assert.Equal(["dock"], AutomationRules.Evaluate(rules, Ctx(fullscreen: true)));
        Assert.Empty(AutomationRules.Evaluate(rules, Ctx(hour: 8)));
    }

    [Fact]
    public void 无法解析的取值不成立()
    {
        var rules = new[] { Rule("dock",
            [Cond(RuleConditionKind.HourOfDay, RuleMatchMode.AtLeast, "abc")]) };
        Assert.Empty(AutomationRules.Evaluate(rules, Ctx(hour: 23)));
    }

    [Fact]
    public void 引擎边沿触发不重复执行()
    {
        var engine = new AutomationEngine();
        var rules = new[] { Rule("dock",
            [Cond(RuleConditionKind.Fullscreen, RuleMatchMode.Equals, "True")]) };
        Assert.Equal(["dock"], engine.Update(rules, Ctx(fullscreen: true)));
        Assert.Empty(engine.Update(rules, Ctx(fullscreen: true)));   // 持续成立不重复
        Assert.Empty(engine.Update(rules, Ctx(fullscreen: false)));  // 不成立不触发
        Assert.Equal(["dock"], engine.Update(rules, Ctx(fullscreen: true))); // 重新成立再触发
    }

    [Fact]
    public void 引擎对同动作多规则只触发一次()
    {
        var engine = new AutomationEngine();
        var rules = new[]
        {
            Rule("dock", [Cond(RuleConditionKind.Fullscreen, RuleMatchMode.Equals, "True")]),
            Rule("dock", [Cond(RuleConditionKind.HourOfDay, RuleMatchMode.AtLeast, "0")]),
        };
        Assert.Equal(["dock"], engine.Update(rules, Ctx(fullscreen: true)));
    }

    [Fact]
    public void 规则被编辑后下一轮重新对齐()
    {
        var engine = new AutomationEngine();
        var on = new[] { Rule("dock", [Cond(RuleConditionKind.Fullscreen, RuleMatchMode.Equals, "True")]) };
        Assert.Equal(["dock"], engine.Update(on, Ctx(fullscreen: true)));
        // 规则整体被替换(编辑/删除后重建) → 已触发集合随下一轮求值重新对齐
        Assert.Empty(engine.Update([], Ctx(fullscreen: true)));
        Assert.Equal(["dock"], engine.Update(on, Ctx(fullscreen: true)));
    }

    [Fact]
    public void 条件枚举以字符串形式往返()
    {
        var rules = new List<AutomationRuleConfig> { Rule("dock",
            [Cond(RuleConditionKind.LessonState, RuleMatchMode.NotEquals, "Breaking")]) };
        var json = System.Text.Json.JsonSerializer.Serialize(rules, ConfigJsonContext.Default.Options);
        Assert.Contains("\"LessonState\"", json);
        Assert.Contains("\"NotEquals\"", json);

        var back = System.Text.Json.JsonSerializer.Deserialize<List<AutomationRuleConfig>>(json, ConfigJsonContext.Default.Options);
        Assert.NotNull(back);
        Assert.Equal(RuleConditionKind.LessonState, back![0].Conditions[0].Kind);
        Assert.Equal(RuleMatchMode.NotEquals, back[0].Conditions[0].Mode);
    }
}
