// 外观设置页 (M2): axaml + AppearanceSettingsViewModel 双向绑定。
// 颜色走 ThemeResources + DynamicResource; 本文件只做控件生命周期与 VM 暴露。
using Avalonia;
using Avalonia.Controls;
using SmartSideBAR.Avalonia.Ui.Settings.ViewModels;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Ui;

public sealed partial class AppearancePage : UserControl
{
    public AppearancePage() : this(null) { }

    public AppearancePage(AppearanceSettingsViewModel? vm)
    {
        InitializeComponent();
        ThemeCard.CornerRadius = FeelCard.CornerRadius =
            new CornerRadius(DesignTokens.Current.Shape.RadiusM);
        DataContext = vm ?? new AppearanceSettingsViewModel();
        // 独立构造（测试/设计器）时先套一份默认调色板，宿主随后会覆盖
        ApplyPalette(AppTheme.From(null));
    }

    public AppearanceSettingsViewModel Vm => (AppearanceSettingsViewModel)DataContext!;

    /// <summary>主题/强调色变化时重刷页面资源 (控件通过 DynamicResource 自动更新)。</summary>
    public void ApplyPalette(Palette p) => ThemeResources.Apply(Resources, p);
}
