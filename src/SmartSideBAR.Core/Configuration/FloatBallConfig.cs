// 与 shared/floatball-layout.ts 同构 —— 配置 + 钳制; 布局算法见 FloatBall/FloatBallLayout.cs
namespace SmartSideBAR.Core.Configuration;

public sealed record FloatBallConfig
{
    public bool Enabled { get; init; } = true;
    public int Size { get; init; } = 56;
    public double IdleOpacity { get; init; } = 0.55;
    public int IdleDelayMs { get; init; } = 4000;
    public int SnapThreshold { get; init; } = 24;
    /// <summary>相对 workArea 的偏移; &lt;0 表示未设置 → 默认位置</summary>
    public int X { get; init; } = -1;
    public int Y { get; init; } = -1;
    public List<string> Actions { get; init; } = ["capture", "annotate", "record", "ime", "longshot", "sidebar"];
    public string Hotkey { get; init; } = "Alt+Q";
    public string DoubleClick { get; init; } = "toggleSidebar"; // toggleSidebar|capture|none
}

public static class FloatBallNormalize
{
    public static readonly string[] KnownActions =
        ["capture", "annotate", "longshot", "record", "ime", "taskmgr", "sidebar", "settings"];

    public static FloatBallConfig Clamp(FloatBallConfig? input, FloatBallConfig? baseValue = null)
    {
        var b = baseValue ?? new FloatBallConfig();
        if (input is null) return b;
        var actions = input.Actions is { Count: > 0 }
            ? input.Actions.Distinct().Where(KnownActions.Contains).Take(8).ToList()
            : b.Actions;
        return input with
        {
            Size = (int)Math.Round(AppearanceNormalize.ClampNum(input.Size, 40, 96, b.Size)),
            IdleOpacity = AppearanceNormalize.ClampNum(input.IdleOpacity, 0.15, 1, b.IdleOpacity),
            IdleDelayMs = (int)Math.Round(AppearanceNormalize.ClampNum(input.IdleDelayMs, 0, 60000, b.IdleDelayMs)),
            SnapThreshold = (int)Math.Round(AppearanceNormalize.ClampNum(input.SnapThreshold, 0, 120, b.SnapThreshold)),
            X = double.IsFinite((double)input.X) ? input.X : b.X,
            Y = double.IsFinite((double)input.Y) ? input.Y : b.Y,
            Actions = actions.Count > 0 ? actions : [.. (baseValue ?? new FloatBallConfig()).Actions],
            Hotkey = string.IsNullOrWhiteSpace(input.Hotkey) ? b.Hotkey : input.Hotkey.Trim(),
            DoubleClick = input.DoubleClick is "toggleSidebar" or "capture" or "none" ? input.DoubleClick : b.DoubleClick,
        };
    }
}
