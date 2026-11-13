// 配置脏标记：未变化的键不落盘、不广播（借鉴 SidebarDiagnostics 的 IsChanged）。
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;

namespace SmartSideBAR.Core.Tests;

public sealed class SetIfChangedTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-dirty-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private ConfigService MakeService(IEventBus? bus = null)
    {
        var paths = new ConfigPaths(
            UserConfigPath: Path.Combine(_dir, "user", "config.json"),
            ProgramDataConfigPath: Path.Combine(_dir, "pd", "policy.json"),
            LegacyUserConfigPath: null,
            LegacyProgramDataConfigPath: null);
        return new ConfigService(paths, bus);
    }

    [Fact]
    public void 值未变化时不写入文件()
    {
        var svc = MakeService();
        svc.Load();
        var path = Path.Combine(_dir, "user", "config.json");
        var before = File.GetLastWriteTimeUtc(path);

        Assert.False(svc.SetIfChanged("capture.format", "PNG")); // 默认值就是 PNG

        Assert.Equal(before, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void 值变化时写入并通知订阅者()
    {
        var bus = new EventBus();
        var fired = 0;
        bus.Subscribe<ConfigChanged>(_ => fired++);
        var svc = MakeService(bus);
        svc.Load();
        var afterLoad = fired; // Load 本身会广播一次，不能用 >0 判断

        Assert.True(svc.SetIfChanged("capture.format", "JPG"));
        Assert.Equal("JPG", svc.Current.Capture.Format);
        Assert.True(fired > afterLoad);
    }

    [Fact]
    public void 第一次变化后再写相同值不再落盘()
    {
        var svc = MakeService();
        svc.Load();
        Assert.True(svc.SetIfChanged("printer.pollIntervalSec", 20));
        Assert.False(svc.SetIfChanged("printer.pollIntervalSec", 20));
        Assert.Equal(20, svc.Current.Printer.PollIntervalSec);
    }

    [Fact]
    public void 未知键仍被白名单拒绝()
    {
        var svc = MakeService();
        svc.Load();
        Assert.False(svc.SetIfChanged("capture.notARealKey", 1));
    }

    [Fact]
    public void 锁定键仍被拒绝()
    {
        var svc = MakeService();
        svc.Load();
        svc.Subscriptions.Lock("capture.");
        Assert.False(svc.SetIfChanged("capture.format", "JPG"));
    }

    [Fact]
    public void 空值恢复正常走Set语义()
    {
        var svc = MakeService();
        svc.Load();
        Assert.True(svc.SetIfChanged("capture.dir", (string?)null));
        // RefreshTyped 会把 {Pictures} 展开成绝对路径，所以断言"已回落且占位符已展开"
        Assert.False(string.IsNullOrWhiteSpace(svc.Current.Capture.Dir));
        Assert.EndsWith("Sidekick", svc.Current.Capture.Dir);
        Assert.DoesNotContain("{Pictures}", svc.Current.Capture.Dir);
    }
}
