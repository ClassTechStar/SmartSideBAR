// 外观设置页代码后置 (P2-b 迁移样板): 控件状态读写 + 调色注入。
// 语义与旧 BuildAppearancePage 逐控件一致, Save 映射键不变; 新增高对比度开关 (P2-a)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Ui;

public sealed partial class AppearancePage : UserControl
{
    private static readonly string[] AccentHex = ["auto", "#2B6EE0", "#12A594", "#7C5CFF", "#D98324", "#4A5568"];

    public AppearancePage()
    {
        InitializeComponent();
        ThemeCard.CornerRadius = FeelCard.CornerRadius =
            new CornerRadius(DesignTokens.Current.Shape.RadiusM);
        ThemeCombo.ItemsSource = new[] { "浅色（默认）", "深色", "自动（跟随系统）" };
        ThemeCombo.SelectedIndex = 0;
        AccentCombo.ItemsSource = new[] { "跟随系统（推荐）", "品牌蓝 #2B6EE0", "青 #12A594", "紫 #7C5CFF", "橙 #D98324", "石墨 #4A5568" };
        AccentCombo.SelectedIndex = 0;
    }

    public void LoadFrom(SidekickConfig c)
    {
        ThemeCombo.SelectedIndex = c.Appearance.Theme.ToLowerInvariant() switch
        {
            "dark" => 1,
            "auto" => 2,
            _ => 0,
        };
        GlassCheck.IsChecked = c.Appearance.LiquidGlass;
        ReduceMotionCheck.IsChecked = c.Appearance.ReduceMotion;
        HighContrastCheck.IsChecked = c.Appearance.HighContrast;
        var accentIdx = Array.FindIndex(AccentHex,
            h => string.Equals(h, c.Appearance.Accent, StringComparison.OrdinalIgnoreCase));
        AccentCombo.SelectedIndex = accentIdx < 0 ? 1 : accentIdx; // 未知值回退品牌蓝（index 1）
    }

    public string ThemeValue => ThemeCombo.SelectedIndex switch
    {
        1 => "dark",
        2 => "auto", // auto 当前按浅色渲染 (AppTheme.IsDark 仅显式 dark 走深色)
        _ => "light",
    };

    public string AccentValue => AccentHex[Math.Clamp(AccentCombo.SelectedIndex, 0, AccentHex.Length - 1)];

    public bool LiquidGlass => GlassCheck.IsChecked == true;

    public bool ReduceMotion => ReduceMotionCheck.IsChecked == true;

    public bool HighContrast => HighContrastCheck.IsChecked == true;

    /// <summary>主题/强调色变化时重刷配色 (与设置窗共用同一 Palette)。</summary>
    public void ApplyPalette(Palette p)
    {
        TitleText.Foreground = p.BodyBrush;
        ThemeTitle.Foreground = p.MutedBrush;
        FeelTitle.Foreground = p.MutedBrush;
        ThemeLabel.Foreground = p.BodyBrush;
        AccentLabel.Foreground = p.BodyBrush;
        ThemeDesc.Foreground = p.MutedBrush;
        AccentDesc.Foreground = p.MutedBrush;
        GlassDesc.Foreground = p.MutedBrush;
        MotionDesc.Foreground = p.MutedBrush;
        ContrastDesc.Foreground = p.MutedBrush;
        foreach (var card in new[] { ThemeCard, FeelCard })
        {
            card.Background = new SolidColorBrush(p.Card);
            card.BorderBrush = p.DividerBrush;
        }
    }
}
