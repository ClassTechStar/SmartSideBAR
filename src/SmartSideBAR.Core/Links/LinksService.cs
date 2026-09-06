// Core/Links/LinksService.cs —— E1/P1-5 语义固化: 过滤仅作用于读取视图, 保存全量回写。
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Links;

public sealed class LinksService(ConfigService config)
{
    /// <summary>侧栏展示视图: 仅 enabled 且 url 非空 (排序保持存储顺序)。</summary>
    public IReadOnlyList<LinkItem> List() => config.Current.Links
        .Where(l => l.Enabled && !string.IsNullOrWhiteSpace(l.Url))
        .ToArray();

    /// <summary>设置界面视图: 全量, 含禁用项。</summary>
    public IReadOnlyList<LinkItem> ListAll() => config.Current.Links.ToArray();

    public LinkItem Add(string name, string url, string? icon = null, bool enabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        var item = new LinkItem
        {
            Id = "l" + Guid.NewGuid().ToString("N")[..10],
            Name = name.Trim(),
            Url = url.Trim(),
            Icon = icon,
            Enabled = enabled,
        };
        Persist(all => { all.Add(item); return true; });
        return item;
    }

    public bool Update(string id, string? name = null, string? url = null, string? icon = null, bool? enabled = null)
    {
        return Persist(all =>
        {
            var idx = all.FindIndex(l => l.Id == id);
            if (idx < 0) return false;
            var item = all[idx] with
            {
                Name = name?.Trim() ?? all[idx].Name,
                Url = url?.Trim() ?? all[idx].Url,
                Icon = icon ?? all[idx].Icon,
                Enabled = enabled ?? all[idx].Enabled,
            };
            all[idx] = item;
            return true;
        });
    }

    public bool Remove(string id) => Persist(all => all.RemoveAll(l => l.Id == id) > 0);

    /// <summary>调整顺序 (目标索引为全量列表索引)。</summary>
    public bool Move(string id, int targetIndex)
    {
        return Persist(all =>
        {
            var idx = all.FindIndex(l => l.Id == id);
            if (idx < 0 || targetIndex < 0 || targetIndex >= all.Count) return false;
            var item = all[idx];
            all.RemoveAt(idx);
            all.Insert(targetIndex, item);
            return true;
        });
    }

    /// <summary>核心不变式 (E1 根修): 变更计算在全量列表上进行, 回写也是全量列表 ——
    /// 禁用/空链接项永不丢失。返回 Persist 运算是否生效。</summary>
    private bool Persist(Func<List<LinkItem>, bool> mutate)
    {
        var all = config.Current.Links.ToList();
        if (!mutate(all)) return false;
        return config.Set("links", all);
    }
}
