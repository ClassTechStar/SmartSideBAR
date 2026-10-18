// UsbPanel —— v1.2 同名面板的 Avalonia 实现。
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

internal sealed class UsbPanel(SidebarServices s, Palette p) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var enabled = Services.Config.Current.Usb.Enabled;
        var root = Stack(12);

        var status = PanelKit.Badge(enabled ? "运行中" : "已停止",
            enabled ? AppTheme.Ok : Palette.Muted,
            enabled ? Color.FromArgb(0x1F, AppTheme.Ok.R, AppTheme.Ok.G, AppTheme.Ok.B) : Palette.Hover);
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        head.Children.Add(new TextBlock
        {
            Text = "USB 监控状态",
            FontSize = 13,
            Foreground = PanelKit.Body("", Palette).Foreground,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(status, 1);
        head.Children.Add(status);
        var refresh = PanelKit.Flat("刷新", Palette, Refresh);
        refresh.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(refresh, 2);
        head.Children.Add(refresh);
        root.Children.Add(head);

        IReadOnlyList<UsbDriveInfo> drives;
        try { drives = Services.Usb.ListRemovable(); }
        catch { drives = []; }

        if (drives.Count == 0)
        {
            root.Children.Add(PanelKit.EmptyState("usb", "暂无 USB 设备连接",
                "插入 U 盘后会自动出现在这里，也可点“刷新”手动检测。", Palette));
        }
        else
        {
            root.Children.Add(PanelKit.Section($"已连接设备（{drives.Count}）", Palette));
            foreach (var d in drives)
            {
                var letter = new TextBlock
                {
                    Text = d.Drive,
                    FontSize = 14,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Palette.AccentBrush,
                };
                var label = PanelKit.Hint(string.IsNullOrWhiteSpace(d.Label) ? "USB 存储设备" : d.Label, Palette);
                // 空读卡器/无介质时 SizeGb 为 0，不要显示成 "0 GB"（看起来像故障）
                var capacity = d.SizeGb > 0 ? $"{d.SizeGb:0.#} GB" : "容量未知";
                var size = PanelKit.Hint($"{capacity} · {(d.Removable ? "可移动磁盘" : "固定磁盘")}", Palette);
                var info = Stack(2);
                info.Children.Add(letter);
                info.Children.Add(label);
                info.Children.Add(size);

                var open = PanelKit.Flat("打开", Palette, () =>
                {
                    try { Services.Shell.OpenPath(d.Drive); }
                    catch (Exception ex) { ToastService.Instance.Show($"打开 {d.Drive} 失败: {ex.Message}", "error"); }
                });

                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
                grid.Children.Add(PanelKit.Icon("usb", Palette, 20));
                Grid.SetColumn(info, 1);
                info.Margin = new Thickness(10, 0, 8, 0);
                grid.Children.Add(info);
                Grid.SetColumn(open, 2);
                grid.Children.Add(open);
                root.Children.Add(PanelKit.Card(grid, Palette, pad: 10));
            }
        }
        return root;
    }
}
