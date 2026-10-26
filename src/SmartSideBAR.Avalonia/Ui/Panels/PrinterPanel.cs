// PrinterPanel, v1.2 同名面板的 Avalonia 实现。
// P2-1：由 697 行的 SidebarPanels.cs 拆出，一面板一文件（单文件 ≤600 行的拆分约定），
// 便于单独单测、减少合并冲突。公共构件见 PanelKit.cs，基类见 SidebarPanel.cs。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Devices;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.Devices;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed class PrinterPanel(SidebarServices s, Palette p) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var root = Stack(12);
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(new TextBlock
        {
            Text = "打印机状态",
            FontSize = 13,
            Foreground = PanelKit.Body("", Palette).Foreground,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var refresh = PanelKit.Flat("刷新", Palette, Refresh);
        Grid.SetColumn(refresh, 1);
        head.Children.Add(refresh);
        root.Children.Add(head);

        IReadOnlyList<PrinterInfo> printers;
        try { printers = Services.Printer.Query(); }
        catch { printers = []; }

        if (printers.Count == 0)
        {
            root.Children.Add(PanelKit.EmptyState("printer", "未检测到打印机",
                "连接打印机后点“刷新”重新检测。", Palette));
            return root;
        }

        foreach (var pr in printers)
        {
            var (text, color) = Describe(pr.State);
            var info = Stack(3);
            info.Children.Add(PanelKit.Body(pr.Name, Palette, 12.5, true));
            info.Children.Add(PanelKit.Row(6,
                PanelKit.Dot(color),
                PanelKit.Hint(text, Palette)));

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            grid.Children.Add(PanelKit.Icon("printer", Palette, 20));
            Grid.SetColumn(info, 1);
            info.Margin = new Thickness(10, 0, 0, 0);
            grid.Children.Add(info);
            root.Children.Add(PanelKit.Card(grid, Palette, pad: 10));
        }
        return root;
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
