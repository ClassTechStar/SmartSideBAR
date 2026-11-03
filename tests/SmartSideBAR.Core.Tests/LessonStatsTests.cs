// 课堂统计回归: 记录/按日分桶/持久化重载/成果包 zip 内容。
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Core.Telemetry;

namespace SmartSideBAR.Core.Tests;

public class LessonStatsTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-stats-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly EventBus _bus = new();
    private readonly LessonStatsService _stats;

    public LessonStatsTests()
    {
        Directory.CreateDirectory(_dir);
        _stats = new LessonStatsService(_bus, _dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    [Fact]
    public void Record_accumulates_per_key()
    {
        _stats.Record("annotate.stroke", 3);
        _stats.Record("annotate.stroke", 5);
        var snap = _stats.Snapshot();
        Assert.Equal(8, snap.Today["annotate.stroke"]);
        Assert.Equal(8, snap.Totals["annotate.stroke"]);
    }

    [Fact]
    public void Bus_events_flow_into_stats()
    {
        _bus.Publish(new StatsRecorded("annotate.ocr", 1, "勾股定理 a2+b2=c2"));
        var snap = _stats.Snapshot();
        Assert.Equal(1, snap.Today["annotate.ocr"]);
        Assert.Contains("勾股定理", snap.Notes[0]);
    }

    [Fact]
    public void Notes_capped_at_200()
    {
        for (var i = 0; i < 210; i++) _stats.Record("annotate.ocr", 1, $"note-{i}");
        Assert.Equal(200, _stats.Snapshot().Notes.Count);
    }

    [Fact]
    public void Stats_persist_across_instances()
    {
        _stats.Record("capture", 2);
        // 新实例从同一 stats.json 加载 (模拟重启)
        var second = new LessonStatsService(_bus, _dir);
        var snap = second.Snapshot();
        Assert.Equal(2, snap.Totals["capture"]);
    }

    [Fact]
    public void ExportLessonPack_creates_zip_with_summary_and_notes()
    {
        var captureDir = Path.Combine(_dir, "cap");
        Directory.CreateDirectory(captureDir);
        File.WriteAllBytes(Path.Combine(captureDir, "annotate_20261104_090000.png"), [1, 2, 3]);
        _stats.Record("annotate.ocr", 1, "板书要点");

        var zipPath = _stats.ExportLessonPack(_dir, captureDir);
        Assert.True(File.Exists(zipPath));

        using var archive = System.IO.Compression.ZipFile.OpenRead(zipPath);
        var names = archive.Entries.Select(e => e.FullName).ToArray();
        Assert.Contains("stats.json", names);
        Assert.Contains("课堂笔记.txt", names);
        Assert.Contains(names, n => n.StartsWith("批注图/"));
    }

    [Fact]
    public void Stats_survive_corrupt_storage()
    {
        File.WriteAllText(Path.Combine(_dir, "stats.json"), "{{{ 不是 JSON");
        var second = new LessonStatsService(_bus, _dir); // 不抛
        second.Record("capture", 1);                     // 且可继续记录
        Assert.Equal(1, second.Snapshot().Totals["capture"]);
    }
}
