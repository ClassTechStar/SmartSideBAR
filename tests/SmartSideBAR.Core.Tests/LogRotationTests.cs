// 日志轮转策略（P2-2）——常驻机上的磁盘占用上限。
// 策略：保留 14 天 + 单文件 10MB 滚动；纯逻辑放 Core 就是为了能在这里钉住。
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Core.Tests;

public class LogRotationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-logrot-" + Guid.NewGuid().ToString("N"));

    public LogRotationTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string Touch(string name, DateTime lastWrite)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, "x");
        File.SetLastWriteTime(path, lastWrite);
        return path;
    }

    [Fact]
    public void Default_budget_is_14_days_and_10mb()
    {
        Assert.Equal(14, LogRotation.DefaultRetentionDays);
        Assert.Equal(10 * 1024 * 1024, LogRotation.DefaultMaxBytes);
    }

    [Fact]
    public void Files_older_than_retention_are_deleted()
    {
        var now = new DateTime(2026, 10, 18);
        var old = Touch("ssb-20260901.log", now.AddDays(-40));
        var recent = Touch("ssb-20261010.log", now.AddDays(-8));
        var today = Touch("ssb-20261018.log", now);

        var removed = LogRotation.Prune(_dir, "ssb-*.log", 14, now);

        Assert.Equal(1, removed);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(recent));
        Assert.True(File.Exists(today));
    }

    [Fact]
    public void Retention_zero_means_keep_everything()
    {
        var now = new DateTime(2026, 10, 18);
        Touch("ssb-20200101.log", now.AddDays(-3000));

        Assert.Equal(0, LogRotation.Prune(_dir, "ssb-*.log", 0, now));
    }

    [Fact]
    public void Missing_directory_is_not_an_error()
    {
        Assert.Equal(0, LogRotation.Prune(Path.Combine(_dir, "nope"), "ssb-*.log", 14, DateTime.Now));
    }

    [Fact]
    public void Roll_triggers_only_at_threshold()
    {
        const long max = 1024;
        Assert.False(LogRotation.ShouldRoll(1023, max));
        Assert.True(LogRotation.ShouldRoll(1024, max));
        Assert.False(LogRotation.ShouldRoll(10, 0)); // 0 = 不限制
    }

    [Fact]
    public void Daily_and_rolled_names_do_not_collide()
    {
        var day = new DateTime(2026, 10, 18);
        Assert.Equal("ssb-20261018.log", LogRotation.DailyName(day));

        var first = LogRotation.RolledName(day, day.AddHours(9).AddMinutes(30).AddSeconds(5));
        var second = LogRotation.RolledName(day, day.AddHours(9).AddMinutes(30).AddSeconds(6));
        Assert.NotEqual(first, second);
        Assert.StartsWith("ssb-20261018_", first);
    }
}
