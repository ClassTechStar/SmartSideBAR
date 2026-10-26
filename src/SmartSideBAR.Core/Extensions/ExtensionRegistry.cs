// 轻量扩展点。
// 借鉴 ClassIsland 的插件清单(PluginManifest)与 Class-Widgets-2 的 API 版本校验 +
// PluginLoadFailure 优雅失败。这里只做"源码级注册点", 不做 AssemblyLoadContext 动态隔离
// —— 动态加载在 PublishTrimmed 下的可用性待确认, 确认前不上。
namespace SmartSideBAR.Core.Extensions;

/// <summary>扩展清单。</summary>
public sealed record ExtensionManifest(
    string Id,
    string Name,
    int ApiVersion,
    string? Version = null,
    string? Description = null);

/// <summary>扩展加载失败记录(借鉴 CW2 的 PluginLoadFailure, 失败可逐个上报而非整体崩)。</summary>
public sealed record ExtensionLoadFailure(string Id, string Reason)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Id) ? "(未命名)" : Id;
}

/// <summary>
/// 扩展注册中心: 内置扩展与外部扩展统一登记, 带 API 版本校验。
/// </summary>
public sealed class ExtensionRegistry
{
    /// <summary>当前宿主 API 版本。</summary>
    public const int CurrentApiVersion = 1;

    private readonly object _gate = new();
    private readonly List<ExtensionManifest> _loaded = new();
    private readonly List<ExtensionLoadFailure> _failures = new();

    public IReadOnlyList<ExtensionManifest> Loaded { get { lock (_gate) return _loaded.ToArray(); } }

    public IReadOnlyList<ExtensionLoadFailure> Failures { get { lock (_gate) return _failures.ToArray(); } }

    public bool TryRegister(ExtensionManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id))
        {
            lock (_gate) _failures.Add(new ExtensionLoadFailure(manifest.Id ?? "", "Id 为空"));
            return false;
        }
        if (manifest.ApiVersion != CurrentApiVersion)
        {
            lock (_gate)
                _failures.Add(new ExtensionLoadFailure(
                    manifest.Id, $"API 版本不匹配: 扩展 {manifest.ApiVersion}, 宿主 {CurrentApiVersion}"));
            return false;
        }
        lock (_gate)
        {
            if (_loaded.Any(e => e.Id == manifest.Id))
            {
                _failures.Add(new ExtensionLoadFailure(manifest.Id, "重复注册"));
                return false;
            }
            _loaded.Add(manifest);
            return true;
        }
    }

    public bool Unregister(string id)
    {
        lock (_gate) return _loaded.RemoveAll(e => e.Id == id) > 0;
    }

    public void Reset()
    {
        lock (_gate)
        {
            _loaded.Clear();
            _failures.Clear();
        }
    }
}

/// <summary>扩展可注册的挂件/面板描述(借鉴 CW2 的 WidgetDefinition)。</summary>
public sealed record ExtensionEntryPoint(
    string ExtensionId,
    string Kind,
    Func<object> Factory);
