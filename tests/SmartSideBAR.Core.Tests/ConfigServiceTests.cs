// Core/Configuration/ConfigServiceTests —— 三层合并/迁移/白名单/钳制/未知字段保留
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;
using System.Text.Json.Nodes;

namespace SmartSideBAR.Core.Tests;

public sealed class ConfigServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-cfg-tests-" + Guid.NewGuid().ToString("N")[..8]);

    private ConfigPaths MakePaths(
        string? userFile = "config.json",
        string? pdFile = "policy.json",
        string? legacyUserFile = null,
        string? legacyPdFile = null)
    {
        return new ConfigPaths(
            UserConfigPath: userFile is null ? Path.Combine(_dir, "missing-dir-x", "c.json") : Path.Combine(_dir, "user", userFile),
            ProgramDataConfigPath: pdFile is null ? Path.Combine(_dir, "missing-dir-y", "p.json") : Path.Combine(_dir, "pd", pdFile),
            LegacyUserConfigPath: legacyUserFile is null ? null : Path.Combine(_dir, "legacy", legacyUserFile),
            LegacyProgramDataConfigPath: legacyPdFile is null ? null : Path.Combine(_dir, "legacy", legacyPdFile));
    }

    [Fact]
    public void Load_NoFiles_WritesDefaultsAndCreatesFile()
    {
        var svc = new ConfigService(MakePaths(userFile: null));
        svc.Load();
        Assert.Equal(SidekickConfig.CurrentVersion, svc.Current.Version);
        Assert.Equal("Microsoft Pinyin", svc.Current.Ime.Slot1);
        Assert.Equal(5, svc.Current.Links.Count);
        Assert.True(svc.Current.Usb.Enabled);
    }

    [Fact]
    public void Load_ThreeLayer_Precedence_PdOverUserOverDefault()
    {
        var paths = MakePaths();
        Directory.CreateDirectory(Path.Combine(_dir, "pd"));
        Directory.CreateDirectory(Path.Combine(_dir, "user"));
        File.WriteAllText(paths.ProgramDataConfigPath, """{"printer":{"pollIntervalSec":30}}""");
        File.WriteAllText(paths.UserConfigPath, """{"ime":{"slot1":"搜狗输入法"}}""");

        var svc = new ConfigService(paths);
        svc.Load();

        Assert.Equal(30, svc.Current.Printer.PollIntervalSec); // 策略层
        Assert.Equal("搜狗输入法", svc.Current.Ime.Slot1);       // 用户层
        Assert.Equal("US", svc.Current.Ime.Slot2);             // 默认层未被覆盖的兄弟字段保留
    }

    [Fact]
    public void Load_NullOverlayValue_IsSkipped_BaseKept()
    {
        var paths = MakePaths();
        Directory.CreateDirectory(Path.Combine(_dir, "user"));
        File.WriteAllText(paths.UserConfigPath, """{"ime":{"slot1":null}}""");
        var svc = new ConfigService(paths);
        svc.Load();
        Assert.Equal("Microsoft Pinyin", svc.Current.Ime.Slot1);
    }

    [Fact]
    public void Load_ArrayOverlay_ReplacesWhole()
    {
        var paths = MakePaths();
        Directory.CreateDirectory(Path.Combine(_dir, "user"));
        File.WriteAllText(paths.UserConfigPath, """{"links":[{"id":"x1","name":"单条","url":"https://a.b","enabled":true}]}""");
        var svc = new ConfigService(paths);
        svc.Load();
        Assert.Single(svc.Current.Links);
        Assert.Equal("x1", svc.Current.Links[0].Id);
    }

    [Fact]
    public void Load_UnknownFields_PreservedAcrossSave()
    {
        var paths = MakePaths();
        Directory.CreateDirectory(Path.Combine(_dir, "user"));
        File.WriteAllText(paths.UserConfigPath,
            """{"futureField":{"a":1},"ime":{"slot1":"搜狗输入法"}}""");
        var svc = new ConfigService(paths);
        svc.Load();
        Assert.True(svc.Set("printer.pollIntervalSec", 42));

        var onDisk = JsonNode.Parse(File.ReadAllText(paths.UserConfigPath))!;
        Assert.NotNull(onDisk["futureField"]);       // 未知名段不丢失 (migrateConfig 语义)
        Assert.Equal(42, (int)onDisk["printer"]!["pollIntervalSec"]!);
    }

    [Fact]
    public void Load_VersionLagged_MigratesAndPersists()
    {
        var paths = MakePaths();
        Directory.CreateDirectory(Path.Combine(_dir, "user"));
        File.WriteAllText(paths.UserConfigPath, """{"version":1}""");
        var svc = new ConfigService(paths);
        svc.Load();
        Assert.Equal(SidekickConfig.CurrentVersion, svc.Current.Version);
        var onDisk = JsonNode.Parse(File.ReadAllText(paths.UserConfigPath))!;
        Assert.Equal(SidekickConfig.CurrentVersion, (int)onDisk["version"]!);
    }

    [Fact]
    public void Load_LegacyUserConfig_MigratedAndRenamed()
    {
        var legacyPath = MakePaths(legacyUserFile: "config.json").LegacyUserConfigPath!;
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, """{"ime":{"slot1":"旧输入法"}}""");

        var svc = new ConfigService(MakePaths(legacyUserFile: "config.json"));
        svc.Load();

        Assert.Equal("旧输入法", svc.Current.Ime.Slot1);
        Assert.False(File.Exists(legacyPath));
        Assert.True(File.Exists(legacyPath + ".migrated.bak"));
    }

    [Fact]
    public void Load_LegacyProgramData_ReadAsPolicyLayer()
    {
        var legacyPd = MakePaths(legacyPdFile: "seewo.json").LegacyProgramDataConfigPath!;
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPd)!);
        File.WriteAllText(legacyPd, """{"printer":{"pollIntervalSec":77}}""");
        var svc = new ConfigService(MakePaths(legacyPdFile: "seewo.json"));
        svc.Load();
        Assert.Equal(77, svc.Current.Printer.PollIntervalSec);
    }

    [Fact]
    public void Load_PathTokens_Expanded()
    {
        var svc = new ConfigService(MakePaths());
        svc.Load();
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        Assert.StartsWith(pictures, svc.Current.Capture.Dir);
        Assert.EndsWith("Sidekick", svc.Current.Capture.Dir);
    }

    [Fact]
    public void Load_ClampsAppearanceAndFloatBall()
    {
        var paths = MakePaths();
        Directory.CreateDirectory(Path.Combine(_dir, "user"));
        File.WriteAllText(paths.UserConfigPath,
            """{"appearance":{"blur":999,"accent":"abc","theme":"neon"},"floatBall":{"size":999,"actions":["capture","bogus","capture"]}}""");
        var svc = new ConfigService(paths);
        svc.Load();
        Assert.Equal(40, svc.Current.Appearance.Blur);
        Assert.Equal("#2B6EE0", svc.Current.Appearance.Accent);
        Assert.Equal("auto", svc.Current.Appearance.Theme);
        Assert.Equal(96, svc.Current.FloatBall.Size);
        Assert.Equal(["capture"], svc.Current.FloatBall.Actions);
    }

    [Fact]
    public void Set_UnknownKey_Rejected()
    {
        var svc = new ConfigService(MakePaths());
        svc.Load();
        Assert.False(svc.Set("evil.section", 1));
        Assert.False(svc.Set("ime.notARealField", 1));
    }

    [Fact]
    public void Set_KnownKey_UpdatesPersistsAndPublishes()
    {
        ConfigChanged? received = null;
        var bus = new EventBus();
        bus.Subscribe<ConfigChanged>(e => received = e);
        var svc = new ConfigService(MakePaths(), bus);
        svc.Load();

        Assert.True(svc.Set("printer.pollIntervalSec", 42));
        Assert.Equal(42, svc.Current.Printer.PollIntervalSec);
        Assert.NotNull(received);
        Assert.Equal(42, received!.Snapshot.Printer.PollIntervalSec);
    }

    [Fact]
    public void SetRemindersIfChanged_OnlyWritesOnActualChange()
    {
        var svc = new ConfigService(MakePaths());
        svc.Load();
        var empty = Array.Empty<Reminder>();
        Assert.False(svc.SetRemindersIfChanged(empty));      // 无变化不落盘 (C1)
        var list = new List<Reminder> { new() { Id = "r1", Kind = ReminderKind.Once, At = 123 } };
        Assert.True(svc.SetRemindersIfChanged(list));
        Assert.False(svc.SetRemindersIfChanged(list));       // 同内容再次跳过
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* 临时目录清理尽力而为 */ }
    }
}
