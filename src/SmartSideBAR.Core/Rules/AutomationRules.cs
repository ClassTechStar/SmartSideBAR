// Ruleset 的配置层消费 (P2-6): automation.rules (持久化) → RuleGroup (求值) → 动作 id。
// 映射与边沿触发都是纯逻辑, 便于单测; WindowManager 只负责在状态变化时喂上下文并 Dispatch。
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Rules;

public static class AutomationRules
{
    /// <summary>把一条持久化规则映射为求值用 RuleGroup; 禁用/无动作 id 的规则返回 null。</summary>
    public static RuleGroup? ToGroup(AutomationRuleConfig rule)
    {
        if (!rule.Enabled || string.IsNullOrWhiteSpace(rule.ActionId)) return null;
        var conditions = rule.Conditions
            .Select(c => new RuleCondition(c.Kind, c.Mode, c.Value))
            .ToArray();
        return new RuleGroup(rule.ActionId.Trim(), conditions, rule.AllMustMatch);
    }

    /// <summary>批量映射, 过滤禁用/无效规则。</summary>
    public static IReadOnlyList<RuleGroup> ToGroups(IEnumerable<AutomationRuleConfig> rules) =>
        rules.Select(ToGroup).Where(g => g is not null).Cast<RuleGroup>().ToArray();

    /// <summary>求值一轮, 返回当前成立的动作 id (去重)。</summary>
    public static IReadOnlyList<string> Evaluate(IEnumerable<AutomationRuleConfig> rules, RuleContext ctx) =>
        Ruleset.Evaluate(ToGroups(rules), ctx);
}

/// <summary>
/// 边沿触发引擎: 只在动作「新成立」时返回一次, 持续成立不重复执行
/// (如「全屏时收起侧栏」不应每分钟收一次)。规则被编辑后下一轮自动重新对齐。
/// </summary>
public sealed class AutomationEngine
{
    private readonly object _gate = new();
    private readonly HashSet<string> _active = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>喂入当前上下文, 返回本轮新触发的动作 id。</summary>
    public IReadOnlyList<string> Update(IEnumerable<AutomationRuleConfig> rules, RuleContext ctx)
    {
        var satisfied = AutomationRules.Evaluate(rules, ctx);
        lock (_gate)
        {
            var triggered = satisfied.Where(a => !_active.Contains(a)).ToArray();
            _active.Clear();
            foreach (var a in satisfied) _active.Add(a);
            return triggered;
        }
    }
}
