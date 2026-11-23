// M1 主题资源化: Palette → ResourceDictionary 的唯一出口。
// 控件通过 {DynamicResource ...} 消费; 主题切换只重写字典, 不再逐控件赋笔刷。
// 渐变胶囊、边光环、BoxShadow 等"形态语言"仍由 ApplyTheme 按玻璃/经典模式处理,
// 这里只收内容色 (背景/描边/文字/交互态)。
using Avalonia.Controls;
using Avalonia.Media;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Ui;

public static class ThemeResources
{
    // 键名集中定义, 避免 XAML 与代码各写一份字符串
    public const string RailHover = "RailHover";
    public const string RailPressed = "RailPressed";
    public const string RailActive = "RailActive";
    public const string BadgeBg = "BadgeBg";
    public const string BadgeFg = "BadgeFg";
    public const string IndicatorBar = "IndicatorBar";
    public const string PanelBg = "PanelBg";
    public const string PanelHeaderBorder = "PanelHeaderBorder";
    public const string PanelTitleFg = "PanelTitleFg";
    public const string PanelCloseFg = "PanelCloseFg";
    public const string BodyFg = "BodyFg";
    public const string MutedFg = "MutedFg";
    public const string CardBg = "CardBg";
    public const string CardBorder = "CardBorder";
    // M5 主题重构: 面板与设置页共用的内容色（单一写入出口, 面板不再各写一份 Palette 映射）
    public const string TextFg = "TextFg";
    public const string CardTextFg = "CardTextFg";
    public const string HoverBg = "HoverBg";
    public const string AccentFg = "AccentFg";
    public const string AccentSoftBg = "AccentSoftBg";
    public const string OnAccentFg = "OnAccentFg";
    public const string OnAccentSoftFg = "OnAccentSoftFg";
    public const string DividerFg = "DividerFg";
    public const string OkFg = "OkFg";
    public const string WarnFg = "WarnFg";
    public const string ErrFg = "ErrFg";
    public const string RecFg = "RecFg";
    public const string RecSoftBg = "RecSoftBg";
    public const string RecBorderFg = "RecBorderFg";

    /// <summary>把调色板写入目标资源字典 (窗口或页面级), 覆盖同名键。</summary>
    public static void Apply(IResourceDictionary target, Palette p)
    {
        ArgumentNullException.ThrowIfNull(target);
        var classic = DesignTokens.Current.Classic;

        target[RailHover] = p.Glass
            ? (IBrush)new SolidColorBrush(p.Dark
                ? Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x14, 0x0F, 0x17, 0x2A))
            : new SolidColorBrush(ParseHex(classic.BrandLight, "#E8F1FC"));
        target[RailPressed] = p.Glass
            ? (IBrush)new SolidColorBrush(p.Dark
                ? Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x21, 0x0F, 0x17, 0x2A))
            : new SolidColorBrush(ParseHex(classic.PressedHover, "#D9E8FB"));
        target[RailActive] = new SolidColorBrush(p.Glass
            ? p.AccentSoft
            : ParseHex(classic.BrandLight, "#E8F1FC"));
        // v1.2 .rail-badge: 品牌色底 + 白字（白底深字在玻璃胶囊上几乎看不见）
        target[BadgeBg] = new SolidColorBrush(p.Accent);
        target[BadgeFg] = new SolidColorBrush(Colors.White);
        // KoBar 选中态指示条: 品牌色竖条
        target[IndicatorBar] = new SolidColorBrush(p.Accent);

        target[PanelBg] = new SolidColorBrush(p.PanelBg);
        target[PanelHeaderBorder] = new SolidColorBrush(p.Divider);
        // v1.2 标题用强调色; 1.0 经典面标题用正文色 (App.vue --text 同源)
        target[PanelTitleFg] = p.Glass
            ? new SolidColorBrush(p.Accent)
            : new SolidColorBrush(p.Text);
        target[PanelCloseFg] = p.Glass
            ? new SolidColorBrush(p.Accent)
            : new SolidColorBrush(p.Muted);

        ApplyContent(target, p);
    }

    /// <summary>
    /// 内容色（设置页 / 侧栏面板共用）。面板构造时也会把这一组写入面板级字典,
    /// 因此侧栏面板既能在窗口内跟随主题, 也能在测试宿主里独立渲染。
    /// </summary>
    public static void ApplyContent(IResourceDictionary target, Palette p)
    {
        ArgumentNullException.ThrowIfNull(target);

        target[BodyFg] = new SolidColorBrush(p.Body);
        target[MutedFg] = new SolidColorBrush(p.Muted);
        target[CardBg] = new SolidColorBrush(p.Card);
        target[CardBorder] = new SolidColorBrush(p.Divider);
        target[TextFg] = new SolidColorBrush(p.Text);
        target[CardTextFg] = new SolidColorBrush(p.CardText);
        target[HoverBg] = new SolidColorBrush(p.Hover);
        target[AccentFg] = new SolidColorBrush(p.Accent);
        target[AccentSoftBg] = new SolidColorBrush(p.AccentSoft);
        target[OnAccentFg] = p.OnAccentBrush;
        var onAccent = p.OnAccentBrush is ISolidColorBrush scb ? scb.Color : Colors.White;
        target[OnAccentSoftFg] = new SolidColorBrush(Color.FromArgb(0xD9, onAccent.R, onAccent.G, onAccent.B));
        target[DividerFg] = new SolidColorBrush(p.Divider);

        target[OkFg] = new SolidColorBrush(AppTheme.Ok);
        target[WarnFg] = new SolidColorBrush(AppTheme.Warn);
        target[ErrFg] = new SolidColorBrush(AppTheme.Err);
        target[RecFg] = new SolidColorBrush(AppTheme.Rec);
        // 录制态强调卡: 红点同源的浅底 + 半透明描边（对应 RecordingCard 的 0x14/0x33）
        var rec = AppTheme.Rec;
        target[RecSoftBg] = new SolidColorBrush(Color.FromArgb(0x14, rec.R, rec.G, rec.B));
        target[RecBorderFg] = new SolidColorBrush(Color.FromArgb(0x33, rec.R, rec.G, rec.B));
    }

    private static Color ParseHex(string? hex, string fallback)
    {
        try { return Color.Parse(hex ?? fallback); }
        catch { return Color.Parse(fallback); }
    }
}
