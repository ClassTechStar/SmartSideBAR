// 主题与调色板 —— 把 config.appearance（light/dark/auto + 强调色 + 玻璃参数）
// 真正落到界面上。v1.2 由 shared/appearance.ts 计算 --lg-* 变量；Avalonia 侧
// 用一份 Palette 记录统一供给周栏、面板与各子面板，避免颜色散落硬编码。
using Avalonia.Media;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Ui;

public sealed record Palette
{
    public required bool Dark { get; init; }
    public required Color PanelBg { get; init; }
    public required Color Text { get; init; }
    public required Color Muted { get; init; }
    public required Color Divider { get; init; }
    public required Color Hover { get; init; }
    public required Color Accent { get; init; }
    public required Color AccentSoft { get; init; }
    /// <summary>rail 胶囊主渐变（上 → 下）。</summary>
    public required Color RailFrom { get; init; }
    public required Color RailTo { get; init; }
    /// <summary>rail 顶光渐隐。</summary>
    public required Color RailGlow { get; init; }
    /// <summary>rail 边光环两端。</summary>
    public required Color RimFrom { get; init; }
    public required Color RimTo { get; init; }

    public IBrush TextBrush => new SolidColorBrush(Text);
    public IBrush MutedBrush => new SolidColorBrush(Muted);
    public IBrush AccentBrush => new SolidColorBrush(Accent);
    public IBrush AccentSoftBrush => new SolidColorBrush(AccentSoft);
    public IBrush DividerBrush => new SolidColorBrush(Divider);
    public IBrush HoverBrush => new SolidColorBrush(Hover);
    public IBrush PanelBrush => new SolidColorBrush(PanelBg);
}

public static class AppTheme
{
    // 状态色（与 v1.2 面板一致：正常绿 / 警告橙 / 故障红 / 录制红）
    public static readonly Color Ok = Color.Parse("#2E9E5B");
    public static readonly Color Warn = Color.Parse("#D98324");
    public static readonly Color Err = Color.Parse("#D64545");
    public static readonly Color Rec = Color.Parse("#E74C3C");

    /// <summary>仅显式选择 dark 才走深色；auto/light 一律浅色（保持 v1.2 出厂的白色玻璃观感）。</summary>
    public static bool IsDark(string mode) =>
        string.Equals(mode, "dark", StringComparison.OrdinalIgnoreCase);

    public static Palette From(AppearanceConfig? appearance)
    {
        var a = AppearanceNormalize.Clamp(appearance);
        var dark = IsDark(a.Theme);
        var accent = ToColor(a.Accent);
        return dark ? Dark(accent, a) : Light(accent, a);
    }

    private static Color ToColor(string hex)
    {
        try { return Color.Parse(AppearanceNormalize.NormalizeAccent(hex)); }
        catch { return Color.Parse("#2B6EE0"); }
    }

    private static Palette Light(Color accent, AppearanceConfig a) => new()
    {
        Dark = false,
        PanelBg = Color.Parse("#FFFFFF"),
        Text = Color.Parse("#2B6EE0"),
        Muted = Color.Parse("#5A6478"),
        Divider = Color.FromArgb(0x12, 0, 0, 0),
        Hover = Color.FromArgb(0x0D, 0x10, 0x20, 0x40),
        Accent = accent,
        AccentSoft = Color.FromArgb(0x14, accent.R, accent.G, accent.B),
        RailFrom = WithAlpha(Color.Parse("#FFFFFF"), (byte)(170 + a.Opacity * 60)),
        RailTo = WithAlpha(Color.Parse("#FFFFFF"), (byte)(100 + a.Opacity * 60)),
        RailGlow = Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF),
        RimFrom = Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF),
        RimTo = Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF),
    };

    private static Palette Dark(Color accent, AppearanceConfig a) => new()
    {
        Dark = true,
        PanelBg = Color.Parse("#171B22"),
        Text = Lighten(accent, 0.35),
        Muted = Color.Parse("#9AA4B6"),
        Divider = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF),
        Hover = Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF),
        Accent = Lighten(accent, 0.2),
        AccentSoft = Color.FromArgb(0x2A, accent.R, accent.G, accent.B),
        RailFrom = Color.FromArgb(0x8C, 0x16, 0x1A, 0x21),
        RailTo = Color.FromArgb(0x70, 0x0C, 0x0F, 0x14),
        RailGlow = Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF),
        RimFrom = Color.FromArgb(0x3D, 0xFF, 0xFF, 0xFF),
        RimTo = Color.FromArgb(0x1F, 0xFF, 0xFF, 0xFF),
    };

    private static Color WithAlpha(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

    private static Color Lighten(Color c, double amount) => Color.FromArgb(
        c.A,
        (byte)Math.Min(255, c.R + (255 - c.R) * amount),
        (byte)Math.Min(255, c.G + (255 - c.G) * amount),
        (byte)Math.Min(255, c.B + (255 - c.B) * amount));
}
