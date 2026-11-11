// 迭代新增配置键的往返与钳制（10-27/10-29 落地的字段此前无单测覆盖）。
// 覆盖：全屏排除名单、多屏索引、诊断开关、临时日程调整、提醒重复规则串。
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Scheduling;
using System.Text.Json.Nodes;

namespace SmartSideBAR.Core.Tests;

public sealed class ConfigNewKeysTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-cfg-new-" + Guid.NewGuid().ToString("N")[..8]);

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
    public void 默认值符合预期()
    {
        var svc = MakeService();
        svc.Load();
        var d = svc.Current.Display;
        Assert.Contains("explorer", d.ExcludedFullscreenWindows);
        Assert.Null(d.MonitorIndex);
        Assert.True(svc.Current.Diagnostics.MemoryWatchDogEnabled);
        Assert.Equal(800, svc.Current.Diagnostics.MemoryLimitMb);
        Assert.Empty(svc.Current.Overrides);
    }

    [Fact]
    public void 全屏排除名单可写可读()
    {
        var svc = MakeService();
        svc.Load();
        Assert.True(svc.Set("display.excludedFullscreenWindows", new[] { "explorer", "notepad" }));
        Assert.Equal(["explorer", "notepad"], svc.Current.Display.ExcludedFullscreenWindows);
    }

    [Fact]
    public void 多屏索引可写可读()
    {
        var svc = MakeService();
        svc.Load();
        Assert.True(svc.Set("display.monitorIndex", 1));
        Assert.Equal(1, svc.Current.Display.MonitorIndex);
    }

    [Fact]
    public void 诊断开关与阈值可写()
    {
        var svc = MakeService();
        svc.Load();
        Assert.True(svc.Set("diagnostics.memoryWatchDogEnabled", false));
        Assert.True(svc.Set("diagnostics.memoryLimitMb", 512));
        Assert.False(svc.Current.Diagnostics.MemoryWatchDogEnabled);
        Assert.Equal(512, svc.Current.Diagnostics.MemoryLimitMb);
    }

    [Fact]
    public void 临时日程调整可写可读()
    {
        var svc = MakeService();
        svc.Load();
        var overrideItem = new ScheduleOverride("o1", DateEpochDay: 20000, Skip: true);
        Assert.True(svc.Set("overrides", new[] { overrideItem }));
        Assert.Single(svc.Current.Overrides);
        Assert.Equal("o1", svc.Current.Overrides[0].Id);
    }

    [Fact]
    public void 提醒重复规则串可写()
    {
        var svc = MakeService();
        svc.Load();
        var reminder = new Reminder
        {
            Id = "r1",
            Kind = ReminderKind.Once,
            At = 1_000L,
            Recurrence = "weekly|1|1|480",
        };
        Assert.True(svc.SetRemindersIfChanged([reminder]));
        Assert.Equal("weekly|1|1|480", svc.Current.Reminders[0].Recurrence);
    }

    [Fact]
    public void 落盘后重新载入保留新键()
    {
        var svc = MakeService();
        svc.Load();
        svc.Set("display.excludedFullscreenWindows", new[] { "explorer", "powerpnt" });
        svc.Set("display.monitorIndex", 2);
        svc.Set("diagnostics.memoryLimitMb", 256);

        var again = MakeService();
        again.Load();
        Assert.Equal(["explorer", "powerpnt"], again.Current.Display.ExcludedFullscreenWindows);
        Assert.Equal(2, again.Current.Display.MonitorIndex);
        Assert.Equal(256, again.Current.Diagnostics.MemoryLimitMb);
    }

    [Fact]
    public void 未知键仍被白名单拒绝()
    {
        var svc = MakeService();
        svc.Load();
        Assert.False(svc.Set("display.notARealKey", 1));
    }
}
