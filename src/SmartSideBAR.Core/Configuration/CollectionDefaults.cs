// 集合型配置的按 Key 自愈合并。
// 借鉴 SidebarDiagnostics 的 MonitorConfig.CheckConfig（Monitoring.cs:2014-2069）：
// 以默认表为骨架，用户表里同 Key 的项保留用户值，缺失的补默认；
// 用户表里多出来的自定义项追加在后面（这是与参考项目的差异 —— 参考项目丢弃未知项，
// 本项目允许用户自定义，不能丢）。
namespace SmartSideBAR.Core.Configuration;

public static class CollectionDefaults
{
    /// <summary>
    /// 用默认集合修补当前集合：
    /// 1) 顺序按默认表；
    /// 2) 同 Key 用当前项覆盖默认项（保留用户改动）；
    /// 3) 当前表里独有的项追加到末尾；
    /// 4) 当前表为 null 时直接返回默认表的副本。
    /// </summary>
    public static IReadOnlyList<T> MergeByKey<T, TKey>(
        IReadOnlyList<T>? current,
        IReadOnlyList<T> defaults,
        Func<T, TKey> keySelector,
        IEqualityComparer<TKey>? comparer = null)
        where TKey : notnull
    {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(keySelector);
        comparer ??= EqualityComparer<TKey>.Default;

        if (current is null || current.Count == 0) return defaults.ToArray();

        var byKey = new Dictionary<TKey, T>(comparer);
        foreach (var item in current)
        {
            var key = keySelector(item);
            byKey.TryAdd(key, item); // 重复 Key 保留第一条，避免不确定覆盖
        }

        var result = new List<T>(defaults.Count + current.Count);
        var used = new HashSet<TKey>(comparer);
        foreach (var def in defaults)
        {
            var key = keySelector(def);
            result.Add(byKey.TryGetValue(key, out var userItem) ? userItem : def);
            used.Add(key);
        }
        foreach (var item in current)
        {
            var key = keySelector(item);
            if (!used.Contains(key)) result.Add(item);
        }
        return result;
    }
}
