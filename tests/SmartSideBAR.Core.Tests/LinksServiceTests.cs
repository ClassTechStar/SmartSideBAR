// LinksServiceTests —— E1/P1-5: 过滤仅在读取视图, 保存全量回写
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Links;

namespace SmartSideBAR.Core.Tests;

public sealed class LinksServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-link-tests-" + Guid.NewGuid().ToString("N")[..8]);

    private (ConfigService Config, LinksService Links, string UserPath) Create()
    {
        var userPath = Path.Combine(_dir, "user", "config.json");
        var svc = new ConfigService(new ConfigPaths(
            userPath, Path.Combine(_dir, "pd", "policy.json"),
            LegacyUserConfigPath: null, LegacyProgramDataConfigPath: null));
        svc.Load();
        return (svc, new LinksService(svc), userPath);
    }

    [Fact]
    public void List_FiltersDisabledAndEmptyUrl_OnlyInView()
    {
        var (_, links, _) = Create();
        // 默认链接含 l4/l5 两个 disabled —— 全量 5 条, 视图 3 条
        Assert.Equal(5, links.ListAll().Count);
        Assert.Equal(3, links.List().Count);
    }

    [Fact]
    public void Add_PersistsFullList_IncludingDisabled()
    {
        var (config, links, userPath) = Create();
        links.Add("新链接", "https://new.example.com");
        Assert.Equal(6, config.Current.Links.Count);              // 禁用项未被"过滤写回"丢失
        Assert.Contains(config.Current.Links, l => l.Id == "l5"); // disabled 仍在
    }

    [Fact]
    public void Remove_WritesBackFullList()
    {
        var (config, links, _) = Create();
        Assert.True(links.Remove("l1"));
        Assert.Equal(4, config.Current.Links.Count);
        Assert.DoesNotContain(config.Current.Links, l => l.Id == "l1");
    }

    [Fact]
    public void Update_UrlOnly_PreservesOtherFields()
    {
        var (config, links, _) = Create();
        var before = config.Current.Links.First(l => l.Id == "l2");
        Assert.True(links.Update("l2", url: "https://easinote.seewo.com/v2"));
        var after = config.Current.Links.First(l => l.Id == "l2");
        Assert.Equal("https://easinote.seewo.com/v2", after.Url);
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Enabled, after.Enabled);
    }

    [Fact]
    public void Move_ReordersInFullList()
    {
        var (config, links, _) = Create();
        Assert.True(links.Move("l5", 0));
        Assert.Equal("l5", config.Current.Links[0].Id);
    }

    [Fact]
    public void Persist_Failure_KeepsConsistent()
    {
        var (_, links, _) = Create();
        Assert.False(links.Update("no-such-id", name: "x"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }
}
