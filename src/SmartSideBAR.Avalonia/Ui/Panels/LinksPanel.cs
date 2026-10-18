// LinksPanel —— v1.2 同名面板的 Avalonia 实现。
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

internal sealed class LinksPanel(SidebarServices s, Palette p, IReadOnlyList<LinkRow> rows) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var root = Stack(6);
        if (rows.Count == 0)
        {
            root.Children.Add(PanelKit.EmptyState("link", "没有启用的链接",
                "在设置中开启需要的教学站点。", Palette));
            return root;
        }
        foreach (var row in rows)
        {
            var btn = new Button
            {
                Content = new TextBlock
                {
                    Text = row.Name,
                    FontSize = 13,
                    Foreground = Palette.AccentBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                Command = row.OpenCommand,
                Padding = new Thickness(12, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Palette.HoverBrush,
                BorderThickness = new Thickness(1),
                BorderBrush = Palette.DividerBrush,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 4),
            };
            root.Children.Add(btn);
        }
        return root;
    }
}
