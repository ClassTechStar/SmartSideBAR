// 历史默认值迁移 (P2-5)。
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Tests;

public class DefaultItemMigrationTests
{
    private static readonly LinkItem Legacy = new()
    {
        Id = "l1",
        Name = "旧课件站",
        Url = "http://old.example.com",
        Icon = "old",
        Enabled = true,
    };

    private static readonly LegacyDefaultRule Rule = new(
        Id: "l1",
        OldName: "旧课件站",
        OldUrl: "http://old.example.com",
        NewName: "课件资源",
        NewUrl: "https://new.example.com",
        NewIcon: "fluent:folder");

    [Fact]
    public void 完全匹配出厂默认值时替换()
    {
        var (items, changed) = DefaultItemMigration.ApplyLinks([Legacy], [Rule]);
        Assert.Equal(1, changed);
        Assert.Equal("课件资源", items[0].Name);
        Assert.Equal("https://new.example.com", items[0].Url);
        Assert.Equal("fluent:folder", items[0].Icon);
        Assert.True(items[0].Enabled); // 未声明的字段保持原值
    }

    [Fact]
    public void 用户已改过名字则不替换()
    {
        var customized = Legacy with { Name = "我自己的名字" };
        var (items, changed) = DefaultItemMigration.ApplyLinks([customized], [Rule]);
        Assert.Equal(0, changed);
        Assert.Equal("我自己的名字", items[0].Name);
    }

    [Fact]
    public void 用户已改过链接则不替换()
    {
        var customized = Legacy with { Url = "https://my.site" };
        var (_, changed) = DefaultItemMigration.ApplyLinks([customized], [Rule]);
        Assert.Equal(0, changed);
    }

    [Fact]
    public void 不在规则内的条目原样返回()
    {
        var other = new LinkItem { Id = "l9", Name = "自定义", Url = "https://x", Enabled = true };
        var (items, changed) = DefaultItemMigration.ApplyLinks([Legacy, other], [Rule]);
        Assert.Equal(1, changed);
        Assert.Equal("l9", items[1].Id);
        Assert.Equal("自定义", items[1].Name);
    }

    [Fact]
    public void 空列表安全返回()
    {
        var (items, changed) = DefaultItemMigration.ApplyLinks(null, [Rule]);
        Assert.Equal(0, changed);
        Assert.Empty(items);
    }
}
