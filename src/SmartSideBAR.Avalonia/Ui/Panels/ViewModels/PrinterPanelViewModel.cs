// M3: 打印机面板 VM（查询 / 状态色映射）。
using System.Collections.ObjectModel;
using Avalonia.Media;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Devices;
using SmartSideBAR.Windows.Devices;

namespace SmartSideBAR.Avalonia.Ui.Panels.ViewModels;

internal sealed record PrinterRow(string Name, string Status, IBrush StatusBrush, IImage? Icon);

internal sealed class PrinterPanelViewModel : PanelViewModel
{
    public PrinterPanelViewModel(SidebarServices services, Palette palette) : base(services, palette)
    {
        RefreshCommand = new RelayCommand(_ => Reload());
    }

    public ObservableCollection<PrinterRow> Printers { get; } = [];

    public bool IsEmpty { get; private set; } = true;

    public IImage? EmptyIcon => SvgIcons.Load("printer", Palette.Dark);

    public RelayCommand RefreshCommand { get; }

    public override void Reload()
    {
        IReadOnlyList<PrinterInfo> printers;
        try { printers = Services.Printer.Query(); }
        catch { printers = []; }

        var icon = SvgIcons.Load("printer", Palette.Dark);
        Printers.Clear();
        foreach (var pr in printers)
        {
            var (text, color) = Describe(pr.State);
            Printers.Add(new PrinterRow(pr.Name, text, Brush(color), icon));
        }

        IsEmpty = Printers.Count == 0;
        Raise(nameof(IsEmpty));
        Raise(nameof(EmptyIcon));
    }

    private static (string Text, Color Color) Describe(PrinterState state) => state switch
    {
        PrinterState.Ok => ("就绪", AppTheme.Ok),
        PrinterState.Idle => ("空闲", AppTheme.Ok),
        PrinterState.OutOfPaper => ("缺纸", AppTheme.Warn),
        PrinterState.LowPaper => ("纸张不足", AppTheme.Warn),
        PrinterState.LowInk => ("墨水不足", AppTheme.Warn),
        PrinterState.Jammed => ("卡纸", AppTheme.Err),
        PrinterState.Offline => ("离线", AppTheme.Err),
        PrinterState.DoorOpen => ("舱门未关", AppTheme.Err),
        PrinterState.ServiceRequired => ("需要维护", AppTheme.Err),
        _ => ("状态未知", AppTheme.Warn),
    };
}
