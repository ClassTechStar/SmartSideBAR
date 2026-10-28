// 面板内容注册表 (P2-1)。
// 借鉴 DiffiTouch 的 contents.py（BaseContent + @load_content 注册进 content_types）
// 与 CyreneCompass 的 items 树（id / 标签 / 图标 / 目标 / children）：
// 把"一个面板一个类"改成"一条声明式定义"，便于与已落地的 ExtensionRegistry 衔接。
namespace SmartSideBAR.Core.Panels;

/// <summary>面板声明（数据化描述，不含 UI 依赖）。</summary>
public sealed record PanelDefinition(
    string Id,
    string Label,
    string? Icon = null,
    string? Action = null,
    bool Enabled = true,
    int Order = 0,
    IReadOnlyList<PanelDefinition>? Children = null);

/// <summary>
/// 面板注册表：按 id 登记，支持顺序调整与启用/禁用（与策略层过滤衔接）。
/// </summary>
public sealed class PanelRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PanelDefinition> _items = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<PanelDefinition> All
    {
        get
        {
            lock (_gate)
                return _items.Values.OrderBy(p => p.Order).ThenBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    /// <summary>注册（同 id 覆盖）。返回是否被拒绝（id 为空）。</summary>
    public bool Register(PanelDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.Id)) return false;
        lock (_gate) _items[definition.Id] = definition;
        return true;
    }

    public bool Unregister(string id)
    {
        lock (_gate) return _items.Remove(id);
    }

    public PanelDefinition? Get(string id)
    {
        lock (_gate) return _items.TryGetValue(id, out var v) ? v : null;
    }

    /// <summary>按禁用名单过滤（与 policy.disabledModules 语义一致）。</summary>
    public IReadOnlyList<PanelDefinition> Visible(IEnumerable<string>? disabled)
    {
        var set = disabled?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>();
        lock (_gate)
            return _items.Values
                .Where(p => p.Enabled && !set.Contains(p.Id))
                .OrderBy(p => p.Order)
                .ThenBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    }

    public void Reset() { lock (_gate) _items.Clear(); }
}
