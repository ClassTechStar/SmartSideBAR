// 规则引擎(条件 → 动作)。
// 借鉴 ClassIsland 的 RulesetService + ActionService: 把"上课时自动静音 USB 提醒"这类
// 自动化从硬编码里解放出来。求值部分为纯逻辑, 便于单测; UI 只负责编辑。
namespace SmartSideBAR.Core.Rules;

public enum RuleConditionKind
{
    /// <summary>时间状态机状态</summary>
    LessonState = 0,
    /// <summary>是否处于全屏</summary>
    Fullscreen = 1,
    /// <summary>一天中的小时(0-23)</summary>
    HourOfDay = 2,
    /// <summary>星期(0=周日)</summary>
    WeekDay = 3,
}

public enum RuleMatchMode
{
    /// <summary>等于</summary>
    Equals = 0,
    /// <summary>不等于</summary>
    NotEquals = 1,
    /// <summary>大于等于(仅数值)</summary>
    AtLeast = 2,
    /// <summary>小于等于(仅数值)</summary>
    AtMost = 3,
}

public sealed record RuleCondition(RuleConditionKind Kind, RuleMatchMode Mode, string Value);

public sealed record RuleGroup(string ActionId, IReadOnlyList<RuleCondition> Conditions, bool AllMustMatch = true);

/// <summary>求值上下文: 由上层把当前运行状态填进来。</summary>
public sealed record RuleContext(
    int LessonState = 0,
    bool Fullscreen = false,
    int HourOfDay = 0,
    int WeekDay = 0);

public static class Ruleset
{
    /// <summary>判断单条条件是否成立。</summary>
    public static bool IsSatisfied(RuleCondition c, RuleContext ctx)
    {
        var actual = c.Kind switch
        {
            RuleConditionKind.LessonState => ctx.LessonState,
            RuleConditionKind.Fullscreen => ctx.Fullscreen ? 1 : 0,
            RuleConditionKind.HourOfDay => ctx.HourOfDay,
            RuleConditionKind.WeekDay => ctx.WeekDay,
            _ => 0,
        };

        // 无法解析的期望值视为不成立
        if (!TryParseExpected(c.Kind, c.Value, out var expected)) return false;

        return c.Mode switch
        {
            RuleMatchMode.Equals => actual == expected,
            RuleMatchMode.NotEquals => actual != expected,
            RuleMatchMode.AtLeast => actual >= expected,
            RuleMatchMode.AtMost => actual <= expected,
            _ => false,
        };
    }

    private static bool TryParseExpected(RuleConditionKind kind, string value, out int expected)
    {
        expected = 0;
        if (kind == RuleConditionKind.Fullscreen)
        {
            if (bool.TryParse(value, out var b)) { expected = b ? 1 : 0; return true; }
            return false;
        }
        if (kind == RuleConditionKind.LessonState)
            return Enum.TryParse<LessonStateForRule>(value, true, out var st)
                && (expected = (int)st) >= 0;
        return int.TryParse(value, out expected);
    }

    /// <summary>求值一组规则, 返回应当执行的动作 id(按组顺序, 去重)。</summary>
    public static IReadOnlyList<string> Evaluate(IEnumerable<RuleGroup> groups, RuleContext ctx)
    {
        var result = new List<string>();
        foreach (var g in groups)
        {
            if (g.Conditions.Count == 0) continue;
            var ok = g.AllMustMatch
                ? g.Conditions.All(c => IsSatisfied(c, ctx))
                : g.Conditions.Any(c => IsSatisfied(c, ctx));
            if (ok && !result.Contains(g.ActionId)) result.Add(g.ActionId);
        }
        return result;
    }
}

/// <summary>规则里使用的时间状态取值(与 Scheduling.LessonState 数值对应)。</summary>
public enum LessonStateForRule
{
    Idle = 0,
    Prepare = 1,
    OnClass = 2,
    Breaking = 3,
    AfterSchool = 4,
}
