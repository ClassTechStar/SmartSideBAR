// QuickSettingsPanel 的 code-behind（M3: 内容移至 QuickSettingsPanel.axaml, 这里只做 VM 接线）。
using Avalonia.Controls;
using SmartSideBAR.Avalonia.Ui.Panels.ViewModels;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Avalonia.Views;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed partial class QuickSettingsPanel : SidebarPanel
{
    public QuickSettingsPanel(SidebarServices services, Palette palette) : base(services, palette)
    {
        PanelTheme.Apply(Resources, palette);
        InitializeComponent();
        Vm = new QuickSettingsPanelViewModel(services, palette) { RefreshRequested = Refresh };
        Vm.ThemeChangeRequested += () => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            (TopLevel.GetTopLevel(this) as SidebarWindow)?.ApplyTheme());
        DataContext = Vm;
    }

    internal QuickSettingsPanelViewModel Vm { get; }

    protected override void RefreshCore()
    {
        Vm.Reload();
        OnBuilt();
    }
}
