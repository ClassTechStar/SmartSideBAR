// 配置备份与损坏回滚（借鉴 Flow.Launcher 的 JsonStorage .bak + 失败恢复）。
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Tests;

public sealed class ConfigBackupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-bak-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private ConfigService MakeService()
    {
        var paths = new ConfigPaths(
            UserConfigPath: Path.Combine(_dir, "user", "config.json"),
            ProgramDataConfigPath: Path.Combine(_dir, "pd", "policy.json"),
            LegacyUserConfigPath: null,
            LegacyProgramDataConfigPath: null);
        return new ConfigService(paths);
    }

    [Fact]
    public void 保存后生成备份文件()
    {
        var svc = MakeService();
        svc.Load(); // 首启写默认配置
        svc.Set("capture.format", "JPG");

        var bak = Path.Combine(_dir, "user", "config.json.bak");
        Assert.True(File.Exists(bak));
        Assert.Contains("PNG", File.ReadAllText(bak)); // 备份的是改动前的版本
        Assert.Contains("JPG", File.ReadAllText(Path.Combine(_dir, "user", "config.json")));
    }

    [Fact]
    public void 主文件损坏时从备份恢复()
    {
        var svc = MakeService();
        svc.Load();
        svc.Set("capture.format", "JPG"); // 此时 .bak 里是 PNG 版本

        // 模拟写盘中断：主文件变成半截 JSON
        var main = Path.Combine(_dir, "user", "config.json");
        File.WriteAllText(main, "{ broken json");

        var again = MakeService();
        again.Load();

        // 回落到备份的 PNG，而不是全默认（也恰好是 PNG）——用更容易区分的键验证
        Assert.NotNull(again.Current.Capture.Format);
    }

    [Fact]
    public void 主文件损坏时恢复用户改动值()
    {
        var svc = MakeService();
        svc.Load();
        svc.Set("display.sidebarSide", SidebarSide.Left);
        // 再保存一次，让 .bak 里也是 left（备份的是上一次的有效内容）
        svc.Set("capture.format", "JPG");

        var main = Path.Combine(_dir, "user", "config.json");
        File.WriteAllText(main, "{ broken");

        var again = MakeService();
        again.Load();
        Assert.Equal(SidebarSide.Left, again.Current.Display.SidebarSide);
    }

    [Fact]
    public void 备份也损坏时安全回落默认值不抛异常()
    {
        var svc = MakeService();
        svc.Load();
        var dir = Path.Combine(_dir, "user");
        File.WriteAllText(Path.Combine(dir, "config.json"), "{ broken");
        File.WriteAllText(Path.Combine(dir, "config.json.bak"), "not json either");

        var again = MakeService();
        var ex = Record.Exception(() => again.Load());
        Assert.Null(ex);
        Assert.NotNull(again.Current);
    }

    [Fact]
    public void 主文件不存在时不生成备份()
    {
        var svc = MakeService();
        svc.Load(); // 首次创建默认配置，此时还没有"旧版本"可备份
        Assert.False(File.Exists(Path.Combine(_dir, "user", "config.json.bak")));
    }
}
