// SettingsPanel 的 code-behind（M3: 内容移至 SettingsPanel.axaml, 这里只做 VM 接线）。
using Avalonia.Controls;
using SmartSideBAR.Avalonia.Ui.Panels.ViewModels;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Avalonia.Views;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed partial class SettingsPanel : SidebarPanel
{
    public SettingsPanel(SidebarServices services, Palette palette) : base(services, palette)
    {
        PanelTheme.Apply(Resources, palette);
        InitializeComponent();
        Vm = new SettingsPanelViewModel(services, palette) { RefreshRequested = Refresh };
        Vm.ThemeChangeRequested += () => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            (TopLevel.GetTopLevel(this) as SidebarWindow)?.ApplyTheme());
        DataContext = Vm;
    }

    internal SettingsPanelViewModel Vm { get; }

    protected override void RefreshCore()
    {
        Vm.Reload();
        OnBuilt();
    }
}
