// 插件契约 v1 (E1): 声明式动作清单模型。
// 只描述"做什么", 不承载代码; 命令类型保留为字符串, 未知类型只废掉单个动作、不废整包
// (前向兼容: 未来新增类型时旧宿主仍能加载同包的其余动作)。
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Actions;

/// <summary>动作清单文件 (actions/*.json)。</summary>
public sealed record ActionPackage(
    int SchemaVersion,
    string Id,
    string Name,
    string? Version = null,
    string? Author = null,
    string? MinHostVersion = null,
    List<ActionDefinition>? Actions = null);

/// <summary>单条动作定义 (展示 + 触发条件 + 命令)。</summary>
public sealed record ActionDefinition(
    string Id,
    string Title,
    string? Description = null,
    string? Icon = null,
    string? Group = null,
    int Order = 0,
    bool Rail = false,
    ActionCommand? Command = null,
    List<AutomationConditionConfig>? Conditions = null,
    bool AllMustMatch = true);

/// <summary>
/// 命令白名单 (v1): open-url / open-path / dispatch / workspace / copy-text / toast。
/// Type 用字符串而不是枚举 —— 未知类型由校验层拒绝, 反序列化不因此整包失败。
/// </summary>
public sealed record ActionCommand(
    string Type,
    string? Url = null,
    string? Path = null,
    string? Action = null,
    string? Workspace = null,
    string? Text = null,
    string? Level = null);

/// <summary>命令类型常量 (供执行器与校验共用)。</summary>
public static class ActionCommandTypes
{
    public const string OpenUrl = "open-url";
    public const string OpenPath = "open-path";
    public const string Dispatch = "dispatch";
    public const string Workspace = "workspace";
    public const string CopyText = "copy-text";
    public const string Toast = "toast";

    public static readonly string[] All = [OpenUrl, OpenPath, Dispatch, Workspace, CopyText, Toast];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(ActionPackage))]
public sealed partial class ActionJsonContext : JsonSerializerContext;
