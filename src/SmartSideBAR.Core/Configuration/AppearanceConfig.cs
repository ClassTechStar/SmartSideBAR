// 与 shared/appearance.ts 同构 —— 仅取纯数据与钳制逻辑; CSS 变量计算留在 UI 层 (Wave C)。
namespace SmartSideBAR.Core.Configuration;

public sealed record AppearanceConfig
{
    public string Theme { get; init; } = "auto";         // light|dark|auto
    public bool LiquidGlass { get; init; } = true;
    public string Material { get; init; } = "acrylic";   // acrylic|mica|blur|none
    public double Blur { get; init; } = 22;
    public double Opacity { get; init; } = 0.62;
    public double Saturate { get; init; } = 1.6;
    public double Refraction { get; init; } = 14;
    public double Specular { get; init; } = 0.55;
    public double Aberration { get; init; } = 2;
    public string Accent { get; init; } = "#2B6EE0";
    public double Radius { get; init; } = 16;
    public bool ReduceMotion { get; init; }
}

public static class AppearanceNormalize
{
    private static readonly string[] ThemeModes = ["light", "dark", "auto"];
    private static readonly string[] Materials = ["acrylic", "mica", "blur", "none"];

    public static double ClampNum(double v, double min, double max, double fallback) =>
        double.IsFinite(v) ? Math.Min(max, Math.Max(min, v)) : fallback;

    public static string NormalizeAccent(string? input, string fallback = "#2B6EE0")
    {
        if (input is null) return fallback;
        var s = input.Trim();
        // 与 appearance.ts 正则一致: #RGB 与 #RRGGBB 均需合法 hex 字符
        if (s.Length == 4 && s[0] == '#' && s[1..].All(char.IsAsciiHexDigit))
        {
            return $"#{s[1]}{s[1]}{s[2]}{s[2]}{s[3]}{s[3]}".ToLowerInvariant();
        }
        if (s.Length == 7 && s[0] == '#' &&
            s[1..].All(c => char.IsAsciiHexDigit(c)))
        {
            return s.ToLowerInvariant();
        }
        return fallback;
    }

    public static AppearanceConfig Clamp(AppearanceConfig? input, AppearanceConfig? baseValue = null)
    {
        var b = baseValue ?? new AppearanceConfig();
        if (input is null) return b;
        return input with
        {
            Theme = ThemeModes.Contains(input.Theme) ? input.Theme : b.Theme,
            LiquidGlass = input.LiquidGlass,
            Material = Materials.Contains(input.Material) ? input.Material : b.Material,
            Blur = ClampNum(input.Blur, 8, 40, b.Blur),
            Opacity = ClampNum(input.Opacity, 0.3, 0.96, b.Opacity),
            Saturate = ClampNum(input.Saturate, 1, 2.2, b.Saturate),
            Refraction = ClampNum(input.Refraction, 0, 40, b.Refraction),
            Specular = ClampNum(input.Specular, 0, 1, b.Specular),
            Aberration = ClampNum(input.Aberration, 0, 8, b.Aberration),
            Accent = NormalizeAccent(input.Accent, b.Accent),
            Radius = ClampNum(input.Radius, 0, 28, b.Radius),
            ReduceMotion = input.ReduceMotion,
        };
    }
}
