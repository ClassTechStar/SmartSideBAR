// 历史默认值迁移 (P2-5)。
// 借鉴 CyreneCompass 的 migrateLegacyDefaults：按「id + 旧文案 + 旧目标」精确匹配后才替换，
// 用户已经改过的条目不会被误伤。
using System.Collections.Generic;

namespace SmartSideBAR.Core.Configuration;

/// <summary>一条旧默认值 → 新默认值的替换规则。</summary>
public sealed record LegacyDefaultRule(
    string Id,
    string? OldName = null,
    string? OldUrl = null,
    string? NewName = null,
    string? NewUrl = null,
    string? NewIcon = null);

public static class DefaultItemMigration
{
    /// <summary>
    /// 对链接条目应用迁移规则。返回新列表与命中条数（原列表不变）。
    /// </summary>
    public static (IReadOnlyList<LinkItem> Items, int Changed) ApplyLinks(
        IReadOnlyList<LinkItem>? items, IEnumerable<LegacyDefaultRule> rules)
    {
        if (items is null || items.Count == 0) return (items ?? [], 0);
        var map = new Dictionary<string, LegacyDefaultRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rules) map[r.Id] = r;

        var changed = 0;
        var result = new List<LinkItem>(items.Count);
        foreach (var item in items)
        {
            if (!map.TryGetValue(item.Id, out var rule))
            {
                result.Add(item);
                continue;
            }
            // 三个条件都匹配才认为"还是出厂默认值"
            var nameMatch = rule.OldName is null || string.Equals(item.Name, rule.OldName, StringComparison.Ordinal);
            var urlMatch = rule.OldUrl is null || string.Equals(item.Url, rule.OldUrl, StringComparison.Ordinal);
            if (!nameMatch || !urlMatch)
            {
                result.Add(item);
                continue;
            }
            result.Add(item with
            {
                Name = rule.NewName ?? item.Name,
                Url = rule.NewUrl ?? item.Url,
                Icon = rule.NewIcon ?? item.Icon,
            });
            changed++;
        }
        return (result, changed);
    }
}
