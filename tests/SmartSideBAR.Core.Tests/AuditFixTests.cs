// 审计修复回归 (全部为本轮真实缺陷的守卫, 修复回退即红)
using System.Text.Json.Nodes;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Core.Telemetry;

namespace SmartSideBAR.Core.Tests;

public class AuditFixTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-audit-" + Guid.NewGuid().ToString("N")[..8]);

    public AuditFixTests()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "pd"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private ConfigService NewConfig(string? userJson = null, string? policyJson = null)
    {
        var policyPath = Path.Combine(_dir, "pd", "policy.json");
        var userPath = Path.Combine(_dir, "user.json");
        if (policyJson is not null) File.WriteAllText(policyPath, policyJson);
        if (userJson is not null) File.WriteAllText(userPath, userJson);
        var paths = new ConfigPaths(
            UserConfigPath: userPath,
            ProgramDataConfigPath: policyPath,
            LegacyUserConfigPath: null,
            LegacyProgramDataConfigPath: null);
        var config = new ConfigService(paths);
        config.Load();
        return config;
    }

    // ---- ConfigService.Load 兜底: schema 损坏不再启动即崩 ----

    [Fact]
    public void Schema损坏的配置回退默认值而非崩溃()
    {
        // printer.pollIntervalSec 类型不符 (手工编辑/互认文件写坏的现实场景)
        var config = NewConfig(userJson: """{"printer": {"pollIntervalSec": "abc"}}""");
        Assert.Equal(10, config.Current.Printer.PollIntervalSec); // schema 默认值
    }

    [Fact]
    public void 被占用的配置文件回退默认值而非崩溃()
    {
        // 模拟"读出非法内容" (真锁文件无法跨进程稳定复现, 用非法 JSON 顶替)
        File.WriteAllText(Path.Combine(_dir, "user.json"), "\uFFFD\uFFFD 不是合法 UTF8 的破坏内容");
        var config = NewConfig(userJson: "]]]not json{{{");
        Assert.NotNull(config.Current);
        Assert.True(config.Current.Version > 0);
    }

    // ---- IsLocked 精确段: {"policy": {}} 导入不再抹掉策略子树 ----

    [Fact]
    public void 锁定键的精确段名也被拒写()
    {
        // 锁在 OOBE 完成后相位生效 (首启向导是 policy 的合法作者) ——
        // 用 completed=true 的用户配置让 Load 时就上锁
        var config = NewConfig(userJson:
            """{"oobe": {"completed": true}, "policy": {"disabledModules": ["usb"]}}""");

        // 精确键 "policy" (不带后缀) 必须同样被锁
        Assert.True(config.Subscriptions.IsLocked("policy"));
        Assert.False(config.Set("policy", new JsonObject()));
        Assert.False(config.Set("policy.disabledModules", Array.Empty<string>()));
        Assert.Contains("usb", config.Current.Policy.DisabledModules); // 子树未被抹掉
    }

    // ---- EnumerateLeaves: 空对象不再产叶 ----

    [Fact]
    public void 导入空对象子树不产叶子()
    {
        var node = JsonNode.Parse("""{"capture": {}, "printer": {"pollIntervalSec": 5}}""")!;
        var leaves = ConfigTransfer.EnumerateLeaves(node).ToList();
        Assert.DoesNotContain(leaves, l => l.Path == "capture");
        Assert.Contains(leaves, l => l.Path == "printer.pollIntervalSec");
    }

    // ---- NextSlotAfter: 古董时间戳算术推进 (此前自旋千万次) ----

    [Fact]
    public void 古董时间戳的间隔提醒算术推进到未来()
    {
        // At=0 (1970) + RepeatMin=1 → 之前是 3000 万次 while 自旋
        var r = new Reminder { Id = "r1", Kind = ReminderKind.Interval, At = 0, RepeatMin = 1 };
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var next = InvokeNextSlotAfter(r, now);
        Assert.True(next > now);
        // 推进粒度恰为一档
        Assert.True(next - now <= 60_000L * 2);
    }

    private static long InvokeNextSlotAfter(Reminder r, long now)
    {
        // NextSlotAfter 是 private —— 通过公共入口 Start 的追赶路径覆盖;
        // 这里用反射调用以精确断言算术结果
        var method = typeof(SchedulerService).GetMethod("NextSlotAfter",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);
        return (long)method!.Invoke(null, [r, now])!;
    }

    // ---- LessonStats: 记录/打包闭环在锁外 I/O 后仍正确 ----

    [Fact]
    public void 统计记录与打包在修复后仍闭环()
    {
        var bus = new EventBus();
        var stats = new LessonStatsService(bus, _dir);
        stats.Record("annotate.stroke", 4);
        stats.Record("annotate.ocr", 1, "板书");
        var snap = stats.Snapshot();
        Assert.Equal(4, snap.Today["annotate.stroke"]);

        var capDir = Path.Combine(_dir, "cap");
        Directory.CreateDirectory(capDir);
        File.WriteAllBytes(Path.Combine(capDir, "annotate_x.png"), [1, 2, 3]);
        var zip = stats.ExportLessonPack(_dir, capDir);
        Assert.True(File.Exists(zip));
        using var archive = System.IO.Compression.ZipFile.OpenRead(zip);
        Assert.Contains(archive.Entries, e => e.FullName == "课堂笔记.txt");
    }
}
