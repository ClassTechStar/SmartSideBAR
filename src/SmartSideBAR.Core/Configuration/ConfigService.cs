// Core/Configuration/ConfigService.cs —— 三层合并 (附录 B.2): 默认 → ProgramData → 用户层
// 语义对齐 v1.2.0 services/config.ts: deepMerge(null 值跳过) / 未知字段保留 / migrateConfig /
// {Pictures} 路径展开 / appearance+floatBall 钳制。持久化以 JsonNode 为准以保留未知字段。
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Messaging;

namespace SmartSideBAR.Core.Configuration;

public sealed record ConfigPaths(
    string UserConfigPath,
    string ProgramDataConfigPath,
    string? LegacyUserConfigPath,
    string? LegacyProgramDataConfigPath)
{
    /// <summary>生产路径: %APPDATA%\SmartSideBAR + %ProgramData%\SmartSideBAR;
    /// 遗留兼容: 开发态 %APPDATA%\smartsidebar (package.json name) 与 %ProgramData%\SeewoSidekick。</summary>
    public static ConfigPaths CreateDefault()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var programData = Environment.GetEnvironmentVariable("ProgramData") ?? @"C:\ProgramData";
        return new ConfigPaths(
            UserConfigPath: Path.Combine(appData, "SmartSideBAR", "config.json"),
            ProgramDataConfigPath: Path.Combine(programData, "SmartSideBAR", "config.json"),
            LegacyUserConfigPath: Path.Combine(appData, "smartsidebar", "config.json"),
            LegacyProgramDataConfigPath: Path.Combine(programData, "SeewoSidekick", "config.json"));
    }
}

public sealed record ConfigChanged(SidekickConfig Snapshot);

public sealed class ConfigService(
    ConfigPaths paths,
    IEventBus? bus = null,
    ILogger<ConfigService>? log = null)
{
    private readonly object _gate = new();
    private JsonNode _root = new JsonObject();
    private SidekickConfig _current = new();

    public SidekickConfig Current { get { lock (_gate) return _current; } }

    /// <summary>持久化根 (含未知字段)。直接改写请走 <see cref="Set"/> 以走白名单与落盘。</summary>
    internal JsonNode RootForTests { get { lock (_gate) return _root.DeepClone(); } }

    public void Load()
    {
        lock (_gate)
        {
            _root = JsonSerializer.SerializeToNode(CreateDefault(), ConfigJsonContext.Default.SidekickConfig)!.DeepClone();

            // 层2 策略层: 新目录优先, 不存在则兼容读取 SeewoSidekick (附录 B.3)
            var policyPath = File.Exists(paths.ProgramDataConfigPath)
                ? paths.ProgramDataConfigPath
                : paths.LegacyProgramDataConfigPath;
            if (policyPath is not null && File.Exists(policyPath))
            {
                if (TryParseNode(File.ReadAllText(policyPath), out var policy))
                {
                    if (policy is JsonObject policyObj) DeepMerge((JsonObject)_root, policyObj);
                    log?.LogInformation("[Config] 已加载策略层 {Path}", policyPath);
                }
                else log?.LogWarning("[Config] 策略层解析失败, 忽略: {Path}", policyPath);
            }

            // 层3 用户层: 首启执行旧 userData 迁移 (旧文件 → 新路径, 旧文件改名 .migrated.bak)
            EnsureUserConfigMigrated();
            if (File.Exists(paths.UserConfigPath))
            {
                if (TryParseNode(File.ReadAllText(paths.UserConfigPath), out var user))
                {
                    if (user is JsonObject userObj) DeepMerge((JsonObject)_root, userObj);
                    log?.LogInformation("[Config] 已加载用户配置 {Path}", paths.UserConfigPath);
                }
                else log?.LogWarning("[Config] 用户配置解析失败, 使用默认值: {Path}", paths.UserConfigPath);
            }
            else
            {
                SaveUserConfigNoLock();
                log?.LogInformation("[Config] 已创建默认用户配置");
            }

            RefreshTypedNoLock();

            // F8/migrateConfig: version 落后时迁移并落盘
            var fromVersion = _current.Version;
            if (fromVersion < SidekickConfig.CurrentVersion)
            {
                _root["version"] = SidekickConfig.CurrentVersion;
                RefreshTypedNoLock();
                SaveUserConfigNoLock();
                log?.LogInformation("[Config] 配置 schema v{From} → v{To} 迁移完成",
                    fromVersion, SidekickConfig.CurrentVersion);
            }

            bus?.Publish(new ConfigChanged(_current));
        }
    }

    /// <summary>按点分路径写入 (如 "capture.format" / "reminderSound.volume")。
    /// B3 根修: 键白名单 —— 路径必须在默认 schema 中存在; 成功后落盘并广播 ConfigChanged。</summary>
    public bool Set(string dotPath, JsonNode? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dotPath);
        lock (_gate)
        {
            if (!IsKnownKey(dotPath))
            {
                log?.LogWarning("[Config] 拒绝写入未知配置键: {Key}", dotPath);
                return false;
            }
            var segments = dotPath.Split('.');
            var node = _root;
            for (var i = 0; i < segments.Length - 1; i++)
            {
                node = node[segments[i]]!; // 白名单已保证存在
            }
            node[segments[^1]] = value?.DeepClone();

            RefreshTypedNoLock();
            var ok = SaveUserConfigNoLock();
            if (ok) bus?.Publish(new ConfigChanged(_current));
            return ok;
        }
    }

    public bool Set<T>(string dotPath, T value)
    {
        if (value is null) return Set(dotPath, (JsonNode?)null);
        var node = value is JsonNode n ? n : JsonSerializer.SerializeToNode(value, typeof(T), ConfigJsonContext.Default.Options);
        return Set(dotPath, node);
    }

    /// <summary>C1 语义: 变更检测落盘 —— 仅 reminders 内容实际变化时写盘。返回是否落盘。</summary>
    public bool SetRemindersIfChanged(IReadOnlyList<Reminder> reminders)
    {
        lock (_gate)
        {
            var before = JsonSerializer.Serialize(_current.Reminders, ConfigJsonContext.Default.Options);
            var after = JsonSerializer.Serialize(reminders, ConfigJsonContext.Default.Options);
            if (before == after) return false;
        }
        return Set("reminders", reminders);
    }

    // ---- 内部 ----

    private void EnsureUserConfigMigrated()
    {
        var legacy = paths.LegacyUserConfigPath;
        if (legacy is null || !File.Exists(legacy) || File.Exists(paths.UserConfigPath)) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(paths.UserConfigPath)!);
            File.Copy(legacy, paths.UserConfigPath);
            File.Move(legacy, legacy + ".migrated.bak");
            log?.LogInformation("[Config] 旧用户配置已迁移: {Legacy} → {New}", legacy, paths.UserConfigPath);
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "[Config] 旧配置迁移失败, 将使用默认值");
        }
    }

    private void RefreshTypedNoLock()
    {
        var typed = _root.Deserialize(ConfigJsonContext.Default.SidekickConfig) ?? new SidekickConfig();
        // 路径变量展开与钳制 (与 config.ts 加载尾段一致)
        typed = typed with
        {
            Capture = typed.Capture with { Dir = ExpandPath(typed.Capture.Dir) },
            Recorder = typed.Recorder with { Dir = ExpandPath(typed.Recorder.Dir) },
            Appearance = AppearanceNormalize.Clamp(typed.Appearance),
            FloatBall = FloatBallNormalize.Clamp(typed.FloatBall),
        };
        _current = typed;
    }

    private bool SaveUserConfigNoLock()
    {
        try
        {
            var dir = Path.GetDirectoryName(paths.UserConfigPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            var tmp = paths.UserConfigPath + ".tmp";
            File.WriteAllText(tmp, _root.ToJsonString(ConfigJsonContext.Default.Options));
            File.Move(tmp, paths.UserConfigPath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            log?.LogError(ex, "[Config] 落盘失败: {Path}", paths.UserConfigPath);
            return false;
        }
    }

    private bool IsKnownKey(string dotPath)
    {
        var node = _root;
        foreach (var seg in dotPath.Split('.'))
        {
            if (node is not JsonObject obj || !obj.ContainsKey(seg)) return false;
            node = obj[seg]!;
        }
        return true;
    }

    private static void DeepMerge(JsonObject target, JsonObject overlay)
    {
        foreach (var (key, overlayVal) in overlay)
        {
            // config.ts 语义: undefined/null 覆盖值跳过, 保留基线
            if (overlayVal is null) continue;
            if (overlayVal is JsonObject overlayObj)
            {
                if (target[key] is JsonObject targetObj) DeepMerge(targetObj, overlayObj);
                else target[key] = overlayObj.DeepClone();
            }
            else
            {
                target[key] = overlayVal.DeepClone(); // 数组/标量整体替换
            }
        }
    }

    private static bool TryParseNode(string json, out JsonNode? node)
    {
        try { node = JsonNode.Parse(json); return node is not null; }
        catch (JsonException) { node = null; return false; }
    }

    private static SidekickConfig CreateDefault() => new()
    {
        Links =
        [
            new LinkItem { Id = "l1", Name = "国家中小学智慧教育平台", Url = "https://www.zxx.edu.cn", Enabled = true },
            new LinkItem { Id = "l2", Name = "希沃白板", Url = "https://easinote.seewo.com", Enabled = true },
            new LinkItem { Id = "l3", Name = "学科网", Url = "https://www.zxxk.com", Enabled = true },
            new LinkItem { Id = "l4", Name = "百度文库", Url = "https://wenku.baidu.com", Enabled = false },
            new LinkItem { Id = "l5", Name = "中国知网", Url = "https://www.cnki.net", Enabled = false },
        ],
    };

    private static string ExpandPath(string p) => p
        .Replace("{Pictures}", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures))
        .Replace("{Videos}", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos))
        .Replace("{Home}", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
}
