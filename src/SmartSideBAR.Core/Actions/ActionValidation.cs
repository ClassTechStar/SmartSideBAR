// 插件契约 v1 (E1): 纯校验规则。
// 所有"能不能执行"的判断都在这里, 便于单测; 执行器只按校验结果分发。
using System.Text.RegularExpressions;

namespace SmartSideBAR.Core.Actions;

public static class ActionValidation
{
    public const int CurrentSchemaVersion = 1;
    /// <summary>单文件上限 256 KB (防解析 DoS)。</summary>
    public const int MaxFileBytes = 256 * 1024;
    public const int MaxActionsPerPackage = 100;
    public const int MaxConditionsPerAction = 8;

    // 允许大小写 (动作 id 常用 camelCase, 如 teaching.openPlatform); 禁止空格与路径分隔符
    private static readonly Regex IdPattern = new(
        "^[A-Za-z0-9][A-Za-z0-9._-]{2,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>保留前缀: 第三方不得冒充内置/系统动作。</summary>
    private static readonly string[] ReservedPrefixes = ["builtin.", "sys."];

    /// <summary>dispatch 允许转发的内置动作子集 (排除更新/安装等敏感动作)。</summary>
    private static readonly HashSet<string> AllowedDispatch = new(StringComparer.OrdinalIgnoreCase)
    {
        "capture", "capture-hotkey",
        "annotate", "annotate-hotkey",
        "longshot", "longshot-hotkey",
        "record", "record-hotkey",
        "ime", "taskmgr", "settings",
        "sidebar", "floatball", "sidebar-toggle", "dock",
        "reminder", "usb", "printer", "miniboard",
    };

    public static bool IsValidId(string? id) => !string.IsNullOrWhiteSpace(id) && IdPattern.IsMatch(id);

    public static bool IsReservedId(string? id) =>
        id is not null && ReservedPrefixes.Any(p => id.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    public static bool IsAllowedUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>
    /// 路径必须落在用户目录或 ProgramData 内; 拒绝相对路径、UNC、`..` 逃逸。
    /// 通过 out normalized 返回展开环境变量后的绝对路径。
    /// </summary>
    public static bool IsAllowedPath(string? raw, out string? normalized,
        string? userRoot = null, string? programDataRoot = null)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        if (raw.StartsWith(@"\\", StringComparison.Ordinal) ||
            raw.StartsWith("//", StringComparison.Ordinal)) return false;

        var expanded = Environment.ExpandEnvironmentVariables(raw.Trim());
        if (!Path.IsPathRooted(expanded)) return false;

        string full;
        try { full = Path.GetFullPath(expanded); }
        catch { return false; }

        var roots = new[]
        {
            userRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            programDataRoot ?? Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData",
        };
        foreach (var root in roots)
        {
            if (string.IsNullOrWhiteSpace(root)) continue;
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.Equals(rootFull, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(rootFull + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                normalized = full;
                return true;
            }
        }
        return false;
    }

    public static bool IsAllowedDispatch(string? action) =>
        !string.IsNullOrWhiteSpace(action) &&
        (AllowedDispatch.Contains(action) ||
         action.StartsWith("workspace:", StringComparison.OrdinalIgnoreCase));

    /// <summary>校验单条命令; 失败给出可读原因 (进 ExtensionRegistry.Failures)。</summary>
    public static bool ValidateCommand(ActionCommand? command, out string? reason)
    {
        reason = null;
        if (command is null) { reason = "缺少 command"; return false; }
        switch (command.Type?.Trim().ToLowerInvariant())
        {
            case ActionCommandTypes.OpenUrl:
                if (!IsAllowedUrl(command.Url)) reason = $"URL 不在允许范围 (仅 http/https): {command.Url}";
                break;
            case ActionCommandTypes.OpenPath:
                if (!IsAllowedPath(command.Path, out _)) reason = $"路径越界或非法: {command.Path}";
                break;
            case ActionCommandTypes.Dispatch:
                if (!IsAllowedDispatch(command.Action)) reason = $"dispatch 动作不在白名单: {command.Action}";
                break;
            case ActionCommandTypes.Workspace:
                if (string.IsNullOrWhiteSpace(command.Workspace) || command.Workspace.Length > 32)
                    reason = "工作区名为空或过长";
                break;
            case ActionCommandTypes.CopyText:
                if (string.IsNullOrEmpty(command.Text)) reason = "复制内容为空";
                else if (command.Text.Length > 4096) reason = "复制内容超过 4KB";
                break;
            case ActionCommandTypes.Toast:
                if (string.IsNullOrWhiteSpace(command.Text) || command.Text.Length > 256) reason = "提示内容为空或过长";
                else if (command.Level is not (null or "info" or "success" or "warn" or "error"))
                    reason = $"提示级别非法: {command.Level}";
                break;
            default:
                reason = $"不支持的命令类型 (当前版本): {command.Type}";
                break;
        }
        return reason is null;
    }

    /// <summary>校验单条动作。</summary>
    public static bool ValidateAction(ActionDefinition action, out string? reason)
    {
        reason = null;
        if (!IsValidId(action.Id)) { reason = $"动作 id 非法: {action.Id}"; return false; }
        if (IsReservedId(action.Id)) { reason = $"动作 id 使用保留前缀: {action.Id}"; return false; }
        if (string.IsNullOrWhiteSpace(action.Title) || action.Title.Length > 64)
        {
            reason = "动作标题为空或超过 64 字"; return false;
        }
        if (action.Description is { Length: > 200 }) { reason = "动作描述超过 200 字"; return false; }
        if (action.Icon is not null && !action.Icon.StartsWith("builtin:", StringComparison.OrdinalIgnoreCase))
        {
            reason = $"v1 图标只允许 builtin: 前缀: {action.Icon}"; return false;
        }
        if (action.Group is { Length: > 32 }) { reason = "分组名超过 32 字"; return false; }
        if (action.Conditions is { Count: > MaxConditionsPerAction })
        {
            reason = $"条件数量超过 {MaxConditionsPerAction}"; return false;
        }
        return ValidateCommand(action.Command, out reason);
    }

    /// <summary>校验包级字段 (动作逐条校验由 Catalog 负责)。</summary>
    public static bool ValidatePackage(ActionPackage package, out string? reason)
    {
        reason = null;
        if (package.SchemaVersion != CurrentSchemaVersion)
        {
            reason = $"不支持的 schemaVersion: {package.SchemaVersion} (当前 {CurrentSchemaVersion})"; return false;
        }
        if (!IsValidId(package.Id)) { reason = $"扩展包 id 非法: {package.Id}"; return false; }
        if (IsReservedId(package.Id)) { reason = $"扩展包 id 使用保留前缀: {package.Id}"; return false; }
        if (string.IsNullOrWhiteSpace(package.Name) || package.Name.Length > 64)
        {
            reason = "扩展包名称为空或超过 64 字"; return false;
        }
        var actions = package.Actions ?? [];
        if (actions.Count == 0) { reason = "扩展包没有任何动作"; return false; }
        if (actions.Count > MaxActionsPerPackage)
        {
            reason = $"动作数量超过 {MaxActionsPerPackage}"; return false;
        }
        return true;
    }
}
