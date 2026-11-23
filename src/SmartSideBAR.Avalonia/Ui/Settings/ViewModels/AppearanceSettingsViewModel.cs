// 外观页 VM: 主题/强调色/质感/动效/高对比度 (颜色仍由 ThemeResources 落到资源字典)。
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

public sealed class AppearanceSettingsViewModel : SettingsPageViewModel
{
    public static readonly string[] ThemeOptions = ["浅色（默认）", "深色", "自动（跟随系统）"];
    public static readonly string[] AccentOptions =
        ["跟随系统（推荐）", "品牌蓝 #2B6EE0", "青 #12A594", "紫 #7C5CFF", "橙 #D98324", "石墨 #4A5568"];
    private static readonly string[] AccentHex = ["auto", "#2B6EE0", "#12A594", "#7C5CFF", "#D98324", "#4A5568"];

    private int _themeIndex;
    private int _accentIndex;
    private bool _liquidGlass;
    private bool _reduceMotion;
    private bool _highContrast;

    /// <summary>XAML 绑定的数据源 (静态数组不能直接绑定)。</summary>
    public IReadOnlyList<string> ThemeOptionsSource => ThemeOptions;

    public IReadOnlyList<string> AccentOptionsSource => AccentOptions;

    public override string Title => "外观";

    public int ThemeIndex { get => _themeIndex; set => SetProperty(ref _themeIndex, value); }

    public int AccentIndex { get => _accentIndex; set => SetProperty(ref _accentIndex, value); }

    public bool LiquidGlass { get => _liquidGlass; set => SetProperty(ref _liquidGlass, value); }

    public bool ReduceMotion { get => _reduceMotion; set => SetProperty(ref _reduceMotion, value); }

    public bool HighContrast { get => _highContrast; set => SetProperty(ref _highContrast, value); }

    public string ThemeValue => ThemeIndex switch
    {
        1 => "dark",
        2 => "auto",
        _ => "light",
    };

    public string AccentValue => AccentHex[Math.Clamp(AccentIndex, 0, AccentHex.Length - 1)];

    public override void LoadFrom(SidekickConfig config)
    {
        ThemeIndex = config.Appearance.Theme.ToLowerInvariant() switch
        {
            "dark" => 1,
            "auto" => 2,
            _ => 0,
        };
        LiquidGlass = config.Appearance.LiquidGlass;
        ReduceMotion = config.Appearance.ReduceMotion;
        HighContrast = config.Appearance.HighContrast;
        var accentIdx = Array.FindIndex(AccentHex,
            h => string.Equals(h, config.Appearance.Accent, StringComparison.OrdinalIgnoreCase));
        AccentIndex = accentIdx < 0 ? 1 : accentIdx; // 未知值回退品牌蓝
    }

    public override IEnumerable<ConfigChange> CollectChanges() =>
    [
        ConfigChange.From("appearance.theme", ThemeValue),
        ConfigChange.From("appearance.liquidGlass", LiquidGlass),
        ConfigChange.From("appearance.reduceMotion", ReduceMotion),
        ConfigChange.From("appearance.highContrast", HighContrast),
        ConfigChange.From("appearance.accent", AccentValue),
    ];
}
