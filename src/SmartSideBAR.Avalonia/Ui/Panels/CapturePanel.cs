// CapturePanel 的 code-behind（M3: 内容移至 CapturePanel.axaml, 这里只做 VM 接线）。
using SmartSideBAR.Avalonia.Ui.Panels.ViewModels;
using SmartSideBAR.Avalonia.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed partial class CapturePanel : SidebarPanel
{
    public CapturePanel(SidebarServices services, Palette palette) : base(services, palette)
    {
        PanelTheme.Apply(Resources, palette);
        InitializeComponent();
        Vm = new CapturePanelViewModel(services, palette) { RefreshRequested = Refresh };
        DataContext = Vm;
    }

    internal CapturePanelViewModel Vm { get; }

    protected override void RefreshCore()
    {
        Vm.Reload();
        OnBuilt();
    }
}
