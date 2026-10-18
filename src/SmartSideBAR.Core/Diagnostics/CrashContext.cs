// 崩溃上下文与安全模式（P2-3）—— 纯逻辑，放 Core 便于单测。
// 目标：崩溃记录可聚合（含版本/DPI/当前面板/OS 版本），且用户能在崩溃后以"安全模式"
// 起一次（禁用悬浮球与 AppBar），把"打不开/一直崩"变成可自助恢复。
using System.Globalization;

namespace SmartSideBAR.Core.Diagnostics;

/// <summary>崩溃发生时的运行上下文，随崩溃记录一起落盘。</summary>
public sealed record CrashContext(
    string Version,
    double DpiScale,
    string ActivePanel,
    string OsBuild)
{
    public static CrashContext Unknown { get; } = new("unknown", 1.0, "none", "unknown");
}

/// <summary>
/// 安全模式：环境变量 SSB_SAFE_MODE=1/true 时生效，禁用悬浮球与 AppBar 注册，
/// 只保留侧栏本体，用于排查"一启动就崩"的场景。
/// </summary>
public static class SafeMode
{
    public const string VariableName = "SSB_SAFE_MODE";

    public static bool IsEnabled(string? raw) =>
        raw is not null &&
        (string.Equals(raw.Trim(), "1", StringComparison.Ordinal) ||
         string.Equals(raw.Trim(), "true", StringComparison.OrdinalIgnoreCase));

    public static bool FromEnvironment() => IsEnabled(Environment.GetEnvironmentVariable(VariableName));

    /// <summary>OS 版本号（如 10.0.26100），用于判定材质/录屏能力降级。</summary>
    public static string OsBuildString(Version? osVersion = null)
    {
        var v = osVersion ?? Environment.OSVersion.Version;
        return v.ToString(3);
    }

    /// <summary>DPI 缩放归一化（96 = 100%）。</summary>
    public static double NormalizeDpi(double rawDpi) =>
        rawDpi is >= 0.5 and <= 8.0 ? Math.Round(rawDpi, 2, MidpointRounding.AwayFromZero) : 1.0;
}
