// 课堂统计 (竞赛叙事: 用数据证明应用价值, 而不是口头说"方便老师")。
// 一切高频动作经 EventBus 汇入: 批注保存/转文字/截图/录屏/长截图/提醒/U 盘。
// 按日分桶 + 累计值, 落盘 stats.json; OCR 文本作为"课堂笔记"一并留存,
// 成果包 (ExportLessonPack) 一键打包批注图 + 笔记 + 统计摘要。
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartSideBAR.Core.Messaging;

namespace SmartSideBAR.Core.Telemetry;

/// <summary>统计埋点事件 —— 高频动作发布, LessonStatsService 订阅汇总。</summary>
public sealed record StatsRecorded(string Key, double Value = 1, string? Note = null);

internal sealed record PersistedStats(Dictionary<string, Dictionary<string, double>>? ByDay, string[]? Notes);

public sealed record LessonStatsSnapshot(
    string TodayKey,
    IReadOnlyDictionary<string, double> Today,
    IReadOnlyDictionary<string, double> Totals,
    IReadOnlyList<string> Notes);

public sealed class LessonStatsService
{
    private const int MaxNotes = 200;
    private readonly object _gate = new();
    private readonly string _storagePath;
    private readonly Dictionary<string, Dictionary<string, double>> _byDay = new();
    private readonly List<string> _notes = []; // 最新在前
    private bool _loaded;

    public LessonStatsService(IEventBus bus, string storageDir)
    {
        _storagePath = Path.Combine(storageDir, "stats.json");
        bus.Subscribe<StatsRecorded>(e => Record(e.Key, e.Value, e.Note));
    }

    public static string TodayKey => DateTime.Now.ToString("yyyy-MM-dd");

    public void Record(string key, double value = 1, string? note = null)
    {
        if (string.IsNullOrEmpty(key)) return;
        lock (_gate)
        {
            EnsureLoaded();
            var day = _byDay.TryGetValue(TodayKey, out var d) ? d : (_byDay[TodayKey] = new Dictionary<string, double>());
            day[key] = day.TryGetValue(key, out var v) ? v + value : value;
            if (!string.IsNullOrWhiteSpace(note))
            {
                _notes.Insert(0, note!);
                if (_notes.Count > MaxNotes) _notes.RemoveAt(_notes.Count - 1);
            }
            Save();
        }
    }

    public LessonStatsSnapshot Snapshot()
    {
        lock (_gate)
        {
            EnsureLoaded();
            var today = _byDay.TryGetValue(TodayKey, out var d)
                ? new Dictionary<string, double>(d)
                : new Dictionary<string, double>();
            return new LessonStatsSnapshot(TodayKey, today, TotalsSnapshot(), _notes.ToArray());
        }
    }

    /// <summary>成果包: 今日批注图 + OCR 课堂笔记 + 统计摘要 → 单个 zip (可直接带走/上交)。</summary>
    public string ExportLessonPack(string outDir, string captureDir)
    {
        lock (_gate)
        {
            EnsureLoaded();
            Directory.CreateDirectory(outDir);
            var path = Path.Combine(outDir, $"LessonPack_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            var today = TodayKey;
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
            var entry = zip.CreateEntry("stats.json");
            using (var w = new StreamWriter(entry.Open()))
            {
                var snapshot = new PersistedStats(
                    new Dictionary<string, Dictionary<string, double>>(
                        _byDay.Where(kv => kv.Key == today).ToDictionary(kv => kv.Key, kv => kv.Value)),
                    _notes.ToArray());
                w.Write(JsonSerializer.Serialize(snapshot, StatsJsonContext.Default.PersistedStats));
            }
            if (_notes.Count > 0)
            {
                var notesEntry = zip.CreateEntry("课堂笔记.txt");
                using (var w = new StreamWriter(notesEntry.Open()))
                {
                    for (var i = 0; i < _notes.Count; i++)
                    {
                        w.WriteLine($"[{i + 1}] {_notes[i]}");
                        w.WriteLine();
                    }
                }
            }
            if (Directory.Exists(captureDir))
            {
                foreach (var file in Directory.EnumerateFiles(captureDir, "*.png"))
                {
                    if (!File.GetLastWriteTime(file).ToString("yyyy-MM-dd").Equals(today)) continue;
                    zip.CreateEntryFromFile(file, "批注图/" + Path.GetFileName(file));
                }
            }
            return path;
        }
    }

    private Dictionary<string, double> TotalsSnapshot()
    {
        var totals = new Dictionary<string, double>();
        foreach (var day in _byDay.Values)
        {
            foreach (var (k, v) in day)
            {
                totals[k] = totals.TryGetValue(k, out var t) ? t + v : v;
            }
        }
        return totals;
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        try
        {
            if (!File.Exists(_storagePath)) return;
            var parsed = JsonSerializer.Deserialize(File.ReadAllText(_storagePath), StatsJsonContext.Default.PersistedStats);
            if (parsed?.ByDay is null) return;
            foreach (var (day, dict) in parsed.ByDay)
            {
                if (dict is not null) _byDay[day] = dict;
            }
            if (parsed.Notes is not null) _notes.AddRange(parsed.Notes.Take(MaxNotes));
        }
        catch
        {
            // 统计损坏不阻断启动
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
            var snapshot = new PersistedStats(
                new Dictionary<string, Dictionary<string, double>>(_byDay),
                _notes.ToArray());
            File.WriteAllText(_storagePath, JsonSerializer.Serialize(snapshot, StatsJsonContext.Default.PersistedStats));
        }
        catch
        {
            // 统计落盘失败不影响功能
        }
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true)]
[JsonSerializable(typeof(PersistedStats))]
internal sealed partial class StatsJsonContext : JsonSerializerContext;
