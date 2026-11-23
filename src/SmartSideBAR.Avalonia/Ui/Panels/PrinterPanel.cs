// PrinterPanel 的 code-behind（M3: 内容移至 PrinterPanel.axaml, 这里只做 VM 接线）。
using SmartSideBAR.Avalonia.Ui.Panels.ViewModels;
using SmartSideBAR.Avalonia.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed partial class PrinterPanel : SidebarPanel
{
    public PrinterPanel(SidebarServices services, Palette palette) : base(services, palette)
    {
        PanelTheme.Apply(Resources, palette);
        InitializeComponent();
        Vm = new PrinterPanelViewModel(services, palette) { RefreshRequested = Refresh };
        DataContext = Vm;
    }

    internal PrinterPanelViewModel Vm { get; }

    protected override void RefreshCore()
    {
        Vm.Reload();
        OnBuilt();
    }
}
