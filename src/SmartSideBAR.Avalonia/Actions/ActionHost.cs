// 插件契约 v1 (E2): 动作宿主 —— 扫描三处 actions/*.json 并接入扩展状态/执行器。
// 扫描顺序: 内置资源 → 用户目录 → ProgramData 策略目录 (优先级递增, 策略可覆盖用户包)。
using System.Text;
using Avalonia.Platform;
using SmartSideBAR.Core.Actions;
using SmartSideBAR.Core.Extensions;

namespace SmartSideBAR.Avalonia.Actions;

public static class ActionHost
{
    private static ActionCatalogResult _catalog = new([], [], []);
    private static ActionExecutor? _executor;

    public static IReadOnlyList<ActionDefinition> Actions => _catalog.Actions;

    public static IReadOnlyList<ActionLoadFailure> Failures => _catalog.Failures;

    public static string UserActionsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartSideBAR", "actions");

    public static string PolicyActionsDir => Path.Combine(
        Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData", "SmartSideBAR", "actions");

    /// <summary>启动时调用 (Avalonia 平台已就绪, 才能读取 avares 资源)。</summary>
    public static void Initialize(string? hostVersion)
    {
        var builtin = new List<ActionSource>();
        var ioFailures = new List<ActionLoadFailure>();
        LoadBuiltin(builtin, ioFailures);

        _catalog = Load(builtin, UserActionsDir, PolicyActionsDir, hostVersion, ioFailures);
        RegisterToExtensionRegistry(_catalog);
    }

    /// <summary>测试入口: 给定内置来源与两个目录, 返回合并结果 (不涉及 avares)。</summary>
    internal static ActionCatalogResult Load(
        IEnumerable<ActionSource> builtin,
        string? userDir,
        string? policyDir,
        string? hostVersion,
        List<ActionLoadFailure>? ioFailures = null)
    {
        var sources = new List<ActionSource>(builtin);
        var failures = ioFailures ?? [];
        LoadDirectory(userDir, ActionSourcePriority.User, sources, failures);
        LoadDirectory(policyDir, ActionSourcePriority.Policy, sources, failures);

        var merged = ActionCatalog.Merge(sources, hostVersion);
        return merged with { Failures = [.. failures, .. merged.Failures] };
    }

    public static void UseExecutor(ActionExecutor executor) => _executor = executor;

    /// <summary>执行已加载的动作。执行器未就绪/校验失败/落地异常都返回 false + 原因。</summary>
    public static bool Execute(ActionDefinition action, out string? error)
    {
        if (_executor is null)
        {
            error = "动作执行器尚未就绪";
            return false;
        }
        return _executor.TryExecute(action, out error);
    }

    internal static void LoadDirectory(
        string? dir, int priority, List<ActionSource> sources, List<ActionLoadFailure> failures)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return;
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            failures.Add(new ActionLoadFailure(dir, "", $"目录读取失败: {ex.Message}"));
            return;
        }

        foreach (var file in files)
        {
            var origin = $"file:{Path.GetFileName(file)}";
            try
            {
                var info = new FileInfo(file);
                if (info.Length > ActionValidation.MaxFileBytes)
                {
                    failures.Add(new ActionLoadFailure(origin, "", "文件超过 256KB 上限"));
                    continue;
                }
                sources.Add(new ActionSource(origin, File.ReadAllText(file, Encoding.UTF8), priority));
            }
            catch (Exception ex)
            {
                failures.Add(new ActionLoadFailure(origin, "", $"读取失败: {ex.Message}"));
            }
        }
    }

    private static void LoadBuiltin(List<ActionSource> sources, List<ActionLoadFailure> failures)
    {
        try
        {
            var folder = new Uri("avares://SmartSideBAR.Avalonia/Assets/actions");
            foreach (var asset in AssetLoader.GetAssets(folder, null).OrderBy(u => u.ToString(), StringComparer.Ordinal))
            {
                using var stream = AssetLoader.Open(asset);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                sources.Add(new ActionSource($"builtin:{Path.GetFileName(asset.LocalPath)}",
                    reader.ReadToEnd(), ActionSourcePriority.Builtin));
            }
        }
        catch (Exception ex)
        {
            failures.Add(new ActionLoadFailure("builtin", "", $"内置动作读取失败: {ex.Message}"));
        }
    }

    private static void RegisterToExtensionRegistry(ActionCatalogResult catalog)
    {
        foreach (var package in catalog.Packages)
        {
            ExtensionHost.Registry.TryRegister(new ExtensionManifest(
                package.Id, package.Name, ExtensionRegistry.CurrentApiVersion, package.Version,
                $"JSON 动作包（{package.Actions?.Count ?? 0} 个动作）"));
        }
        foreach (var failure in catalog.Failures)
        {
            ExtensionHost.Registry.ReportFailure(failure.Id, $"{failure.Origin}: {failure.Reason}");
        }
    }
}
