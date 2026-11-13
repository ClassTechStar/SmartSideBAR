// 集合按 Key 自愈合并（借鉴 SidebarDiagnostics MonitorConfig.CheckConfig）。
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Tests;

public class CollectionDefaultsTests
{
    private static LinkItem L(string id, string name, bool enabled = true) =>
        new() { Id = id, Name = name, Url = $"https://{id}", Enabled = enabled };

    private static readonly LinkItem[] Defaults = [L("a", "默认A"), L("b", "默认B"), L("c", "默认C")];

    [Fact]
    public void 空集合返回全部默认项()
    {
        var merged = CollectionDefaults.MergeByKey(null, Defaults, l => l.Id);
        Assert.Equal(3, merged.Count);
        Assert.Equal(new[] { "a", "b", "c" }, merged.Select(l => l.Id));
    }

    [Fact]
    public void 同Key保留用户值()
    {
        var current = new[] { L("a", "我改过的名字") };
        var merged = CollectionDefaults.MergeByKey(current, Defaults, l => l.Id);
        Assert.Equal("我改过的名字", merged[0].Name);
        Assert.Equal(3, merged.Count);
    }

    [Fact]
    public void 用户自定义项追加在末尾不丢弃()
    {
        var current = new[] { L("a", "A"), L("mine", "我的") };
        var merged = CollectionDefaults.MergeByKey(current, Defaults, l => l.Id);
        Assert.Equal(4, merged.Count);
        Assert.Equal("mine", merged[^1].Id);
    }

    [Fact]
    public void 顺序以默认表为骨架()
    {
        var current = new[] { L("c", "C"), L("a", "A") };
        var merged = CollectionDefaults.MergeByKey(current, Defaults, l => l.Id);
        Assert.Equal(new[] { "a", "b", "c" }, merged.Select(l => l.Id));
    }

    [Fact]
    public void Key比较可指定忽略大小写()
    {
        var current = new[] { L("A", "大写键") };
        var merged = CollectionDefaults.MergeByKey(current, Defaults, l => l.Id, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(3, merged.Count);
        Assert.Equal("大写键", merged[0].Name);
    }

    [Fact]
    public void MergeMissingDefaults_返回新增条数()
    {
        var (items, added) = DefaultItemMigration.MergeMissingDefaults(
            new[] { L("a", "A") }, Defaults);
        Assert.Equal(2, added);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void MergeMissingDefaults_已有全部默认项时不新增()
    {
        var (_, added) = DefaultItemMigration.MergeMissingDefaults(Defaults, Defaults);
        Assert.Equal(0, added);
    }
}
