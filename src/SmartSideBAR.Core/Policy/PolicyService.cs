// Core/Policy/PolicyService.cs —— disabledModules 白名单 (P2-1/P2-3 语义):
// 驱动服务启动跳过 + rail 图标过滤。模块标识与 v1.2.0 对齐。
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Policy;

public static class ModuleIds
{
    public const string Ime = "ime";
    public const string Capture = "capture";
    public const string Annotate = "annotate";
    public const string Longshot = "longshot";
    public const string Recorder = "recorder";
    public const string Usb = "usb";
    public const string Printer = "printer";
    public const string TaskMgr = "taskmgr";
    public const string Links = "links";
    public const string Reminder = "reminder";
    public const string FloatBall = "floatball";
    public const string Settings = "settings";
    public const string Diagnostics = "diagnostics";
}

public sealed class PolicyService(ConfigService config)
{
    public bool IsDisabled(string moduleId) =>
        config.Current.Policy.DisabledModules.Contains(moduleId, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> DisabledModules() => config.Current.Policy.DisabledModules.ToArray();
}
