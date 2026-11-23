// 插件契约 v1 (E1): 动作目录 —— 解析、校验、分层合并。
// 纯逻辑, 不碰文件系统 (文件读取在宿主层); 失败逐条记录, 单包/单动作失败不影响其他扩展。
using System.Text;
using System.Text.Json;
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Core.Actions;

/// <summary>动作来源。Priority 越大越优先 (策略 &gt; 用户 &gt; 内置)。</summary>
public sealed record ActionSource(string Origin, string Json, int Priority);

public static class ActionSourcePriority
{
    public const int Builtin = 0;
    public const int User = 10;
    public const int Policy = 20;
}

public sealed record ActionLoadFailure(string Origin, string Id, string Reason);

public sealed record ActionCatalogResult(
    IReadOnlyList<ActionDefinition> Actions,
    IReadOnlyList<ActionPackage> Packages,
    IReadOnlyList<ActionLoadFailure> Failures);

public static class ActionCatalog
{
    /// <summary>
    /// 合并多个来源。hostVersion 非空时按 minHostVersion 过滤;
    /// 同包 id 由高优先级整体覆盖低优先级 (策略层可替换用户包, 被替换包的动作一并失效)。
    /// </summary>
    public static ActionCatalogResult Merge(IEnumerable<ActionSource> sources, string? hostVersion = null)
    {
        var failures = new List<ActionLoadFailure>();
        var packages = new Dictionary<string, (ActionSource Source, ActionPackage Package)>(StringComparer.OrdinalIgnoreCase);
        var packageActions = new Dictionary<string, List<ActionDefinition>>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources.OrderBy(s => s.Priority).ThenBy(s => s.Origin, StringComparer.Ordinal))
        {
            if (Encoding.UTF8.GetByteCount(source.Json) > ActionValidation.MaxFileBytes)
            {
                failures.Add(new ActionLoadFailure(source.Origin, "", "文件超过 256KB 上限"));
                continue;
            }

            ActionPackage? package;
            try
            {
                package = JsonSerializer.Deserialize(source.Json, ActionJsonContext.Default.ActionPackage);
            }
            catch (JsonException ex)
            {
                failures.Add(new ActionLoadFailure(source.Origin, "", $"JSON 解析失败: {ex.Message}"));
                continue;
            }
            if (package is null)
            {
                failures.Add(new ActionLoadFailure(source.Origin, "", "清单为空"));
                continue;
            }

            if (!ActionValidation.ValidatePackage(package, out var packageReason))
            {
                failures.Add(new ActionLoadFailure(source.Origin, package.Id ?? "", packageReason!));
                continue;
            }

            if (!string.IsNullOrWhiteSpace(package.MinHostVersion) &&
                !string.IsNullOrWhiteSpace(hostVersion) &&
                VersionComparer.IsNewer(package.MinHostVersion, hostVersion))
            {
                failures.Add(new ActionLoadFailure(source.Origin, package.Id,
                    $"需要宿主 v{package.MinHostVersion}，当前 {hostVersion}"));
                continue;
            }

            if (packages.TryGetValue(package.Id, out var existing) && existing.Source.Priority >= source.Priority)
            {
                failures.Add(new ActionLoadFailure(source.Origin, package.Id, "重复的扩展包 id (低优先级被忽略)"));
                continue;
            }

            packages[package.Id] = (source, package);
            var valid = new List<ActionDefinition>();
            foreach (var action in package.Actions ?? [])
            {
                if (!ActionValidation.ValidateAction(action, out var actionReason))
                {
                    failures.Add(new ActionLoadFailure(source.Origin, action.Id ?? "", actionReason!));
                    continue;
                }
                valid.Add(action);
            }
            packageActions[package.Id] = valid;
        }

        // 高优先级包先占 id; 低优先级同 id 动作记失败 (被覆盖包的动作不进入结果)
        var actions = new List<ActionDefinition>();
        var actionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (source, package) in packages.Values
                     .OrderByDescending(p => p.Source.Priority)
                     .ThenBy(p => p.Source.Origin, StringComparer.Ordinal))
        {
            foreach (var action in packageActions[package.Id])
            {
                if (!actionIds.Add(action.Id))
                {
                    failures.Add(new ActionLoadFailure(source.Origin, action.Id, "动作 id 重复 (高优先级包已占用)"));
                    continue;
                }
                actions.Add(action);
            }
        }

        return new ActionCatalogResult(
            actions.OrderBy(a => a.Order).ThenBy(a => a.Id, StringComparer.OrdinalIgnoreCase).ToArray(),
            packages.Values.Select(v => v.Package).ToArray(),
            failures);
    }
}
