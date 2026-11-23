// ConfigService.ApplyMany (2026-11 写放大治理): 批量写入/变更过滤/白名单/锁定键, 以及 .bak 每日滚动。
using System.Text.Json.Nodes;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Tests;

public sealed class ConfigApplyManyTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-cfg-batch-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private string UserConfigPath => Path.Combine(_dir, "user", "config.json");

    private ConfigService MakeService()
    {
        var paths = new ConfigPaths(
            UserConfigPath: UserConfigPath,
            ProgramDataConfigPath: Path.Combine(_dir, "pd", "policy.json"),
            LegacyUserConfigPath: null,
            LegacyProgramDataConfigPath: null);
        return new ConfigService(paths);
    }

    [Fact]
    public void 批量写入一次生效并可重载()
    {
        var svc = MakeService();
        svc.Load();

        var applied = svc.ApplyMany(
        [
            ConfigChange.From("capture.format", "JPG"),
            ConfigChange.From("recorder.fps", 30),
            ConfigChange.From("usb.enabled", false),
        ]);

        Assert.Equal(3, applied);
        Assert.Equal("JPG", svc.Current.Capture.Format);
        Assert.Equal(30, svc.Current.Recorder.Fps);
        Assert.False(svc.Current.Usb.Enabled);

        var again = MakeService();
        again.Load();
        Assert.Equal("JPG", again.Current.Capture.Format);
        Assert.Equal(30, again.Current.Recorder.Fps);
        Assert.False(again.Current.Usb.Enabled);
    }

    [Fact]
    public void 未变化键不计入变化数()
    {
        var svc = MakeService();
        svc.Load();
        Assert.Equal(0, svc.ApplyMany(
        [
            ConfigChange.From("capture.format", svc.Current.Capture.Format),
            ConfigChange.From("recorder.fps", svc.Current.Recorder.Fps),
        ]));
    }

    [Fact]
    public void 未知键跳过有效键照写()
    {
        var svc = MakeService();
        svc.Load();
        var applied = svc.ApplyMany(
        [
            ConfigChange.From("capture.notARealKey", 1),
            ConfigChange.From("usb.enabled", false),
        ]);
        Assert.Equal(1, applied);
        Assert.False(svc.Current.Usb.Enabled);
    }

    [Fact]
    public void 锁定策略段在批量写入中同样被拒()
    {
        var svc = MakeService();
        svc.Load();
        svc.Subscriptions.Lock("policy.");

        Assert.Equal(0, svc.ApplyMany(
            [ConfigChange.From("policy.disabledModules", new[] { "capture" })]));
        Assert.Empty(svc.Current.Policy.DisabledModules);
    }

    [Fact]
    public void 批量写入同样滚动备份()
    {
        var svc = MakeService();
        svc.Load();
        Assert.Equal(1, svc.ApplyMany([ConfigChange.From("recorder.fps", 20)])); // 保存前先备份上一份
        Assert.True(File.Exists(UserConfigPath + ".bak"));
        Assert.Equal(15, ReadBackupFps());
        Assert.Equal(20, svc.Current.Recorder.Fps);
    }

    private int ReadBackupFps()
    {
        var node = JsonNode.Parse(File.ReadAllText(UserConfigPath + ".bak"))!;
        return node["recorder"]!["fps"]!.GetValue<int>();
    }
}
