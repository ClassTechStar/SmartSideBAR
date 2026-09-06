// Core/Configuration/SidekickConfig.cs —— 附录 B.1: 与 v1.2.0 src/shared/types.ts 逐字段同构
// JSON 命名 camelCase, 与 Electron 版 config.json 互认 (ADR-M8)。
using System.Text.Json.Serialization;

namespace SmartSideBAR.Core.Configuration;

public sealed record SidekickConfig
{
    public const int CurrentVersion = 2;

    public int Version { get; init; } = CurrentVersion;
    public ImeConfig Ime { get; init; } = new();
    public CaptureConfig Capture { get; init; } = new();
    public RecorderConfig Recorder { get; init; } = new();
    public UsbConfig Usb { get; init; } = new();
    public PrinterConfig Printer { get; init; } = new();
    public DisplayConfig Display { get; init; } = new();
    public AppearanceConfig Appearance { get; init; } = new();
    public FloatBallConfig FloatBall { get; init; } = new();
    public List<LinkItem> Links { get; init; } = [];
    public List<Reminder> Reminders { get; init; } = [];
    public ReminderSoundConfig ReminderSound { get; init; } = new();
    public OobeState Oobe { get; init; } = new();
    public PolicyConfig Policy { get; init; } = new();
}

public sealed record ImeConfig
{
    public string Slot1 { get; init; } = "Microsoft Pinyin";
    public string Slot2 { get; init; } = "US";
    public bool FallbackSwap { get; init; } = true;
}

public sealed record CaptureConfig
{
    public string Hotkey { get; init; } = "Ctrl+Shift+A";
    public string AnnotateHotkey { get; init; } = "Ctrl+Shift+D";
    public string LongshotHotkey { get; init; } = "Ctrl+Shift+L";
    public string Format { get; init; } = "PNG"; // png|jpg
    public string Dir { get; init; } = "{Pictures}/Sidekick";
}

public sealed record RecorderConfig
{
    public int Fps { get; init; } = 15;
    public string Bitrate { get; init; } = "2M";
    public bool Mic { get; init; }
    public string Dir { get; init; } = "{Videos}/Sidekick";
}

public sealed record UsbConfig
{
    public bool Enabled { get; init; } = true;
    public List<string> IgnoreTypes { get; init; } = ["phone", "carplay"];
}

public sealed record PrinterConfig
{
    public int PollIntervalSec { get; init; } = 10;
}

public sealed record DisplayConfig
{
    public string SidebarMonitor { get; init; } = "primary";
    public SidebarSide SidebarSide { get; init; } = SidebarSide.Right;
    public bool FitWindowsToWorkArea { get; init; } = true;
}

[JsonConverter(typeof(JsonStringEnumConverter<SidebarSide>))]
public enum SidebarSide { Left, Right }

public sealed record PolicyConfig
{
    public List<string> DisabledModules { get; init; } = [];
}

// ---- 与 shared/types.ts 同构的值类型 ----

public sealed record LinkItem
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public string? Icon { get; init; }
    public bool Enabled { get; init; } = true;
}

[JsonConverter(typeof(JsonStringEnumConverter<ReminderKind>))]
public enum ReminderKind { Once, Interval, Hourly }

public sealed record Reminder
{
    public string Id { get; init; } = "";
    public ReminderKind Kind { get; init; } = ReminderKind.Once;
    /// <summary>epoch 毫秒 —— 与 v1.2.0 Reminder.at 语义一致</summary>
    public long At { get; init; }
    public string? Note { get; init; }
    public int? RepeatMin { get; init; }
    public long? SnoozedUntil { get; init; }
}

public sealed record ReminderSoundConfig
{
    public string Preset { get; init; } = "default"; // default|bell|chime|alarm
    public string? Mp3Path { get; init; }
    public double Volume { get; init; } = 0.8;       // 0~1
    public int Repeat { get; init; } = 3;            // 1~5
    public int RepeatInterval { get; init; } = 800;  // ms
}

public sealed record OobePrefs
{
    public bool Ime { get; init; } = true;
    public bool Usb { get; init; } = true;
    public bool Shot { get; init; } = true;
    public bool Recorder { get; init; }
    public bool Printer { get; init; } = true;
}

public sealed record OobeEnvScreen
{
    public int W { get; init; } = 1920;
    public int H { get; init; } = 1080;
    public double Scale { get; init; } = 1;
    public bool Touch { get; init; } = true;
}

public sealed record OobeEnv
{
    public OobeEnvScreen Screen { get; init; } = new();
    public int PrinterCount { get; init; }
    public bool ImeOk { get; init; } = true;
    public string Os { get; init; } = "win32";
}

public sealed record OobeState
{
    public bool Completed { get; init; }
    public string? CompletedAt { get; init; } // ISO 字符串, 与 v1.2.0 一致
    public bool Skipped { get; init; }
    public string? Role { get; init; }        // teacher|admin|null
    public int LastStepIndex { get; init; }
    public OobePrefs Prefs { get; init; } = new();
    public OobeEnv Env { get; init; } = new();
}
