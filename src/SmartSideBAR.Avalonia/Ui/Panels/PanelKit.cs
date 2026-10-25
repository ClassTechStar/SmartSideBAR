// 面板公共构件 —— v1.2 各 Panel 的 CSS 语义在 Avalonia 侧的对应实现。
// 统一 13px 正文 / 12px 辅助 / 12px 圆角卡片 / 2 列动作磁贴，避免每个面板各写一套。
// 圆角/间距取 design-tokens shape/spacing 角色 (Seelen 语义: s=按钮, m=卡片/弹层)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal static class PanelKit
{
    private static Core.Design.ShapeTokens Shape => DesignTokens.Current.Shape;

    public static TextBlock Section(string text, Palette p) => new()
    {
        Text = text,
        FontSize = 12.5,
        FontWeight = FontWeight.SemiBold,
        Foreground = p.MutedBrush,
        Margin = new Thickness(0, 2, 0, 6),
    };

    public static TextBlock Body(string text, Palette p, double size = 13, bool bold = false) => new()
    {
        Text = text,
        FontSize = size,
        FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
        Foreground = new SolidColorBrush(p.Dark ? Color.Parse("#E6EBF3") : Color.Parse("#1B2230")),
        TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Hint(string text, Palette p) => new()
    {
        Text = text,
        FontSize = 12,
        Foreground = p.MutedBrush,
        TextWrapping = TextWrapping.Wrap,
        LineHeight = 18,
    };

    public static Border Card(Control content, Palette p, bool accent = false, double pad = 12)
    {
        var border = new Border
        {
            Background = accent ? p.AccentSoftBrush : p.HoverBrush,
            CornerRadius = new CornerRadius(Shape.RadiusM),
            Padding = new Thickness(pad),
            Child = content,
            BorderThickness = new Thickness(1),
            BorderBrush = accent ? new SolidColorBrush(Color.FromArgb(0x33, p.Accent.R, p.Accent.G, p.Accent.B)) : p.DividerBrush,
        };
        return border;
    }

    public static Border Badge(string text, Color fg, Color bg) => new()
    {
        Background = new SolidColorBrush(bg),
        CornerRadius = new CornerRadius(999),
        Padding = new Thickness(7, 2),
        VerticalAlignment = VerticalAlignment.Center,
        Child = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(fg),
        },
    };

    public static Border Dot(Color color, double size = 8) => new()
    {
        Width = size,
        Height = size,
        CornerRadius = new CornerRadius(size / 2),
        Background = new SolidColorBrush(color),
        VerticalAlignment = VerticalAlignment.Center,
    };

    /// <summary>2 列动作磁贴（对应 v1.2 CapturePanel 的 action-grid）。</summary>
    public static Control TileGrid(Palette p, params (string Icon, string Label, Action Click)[] items)
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            ColumnSpacing = 10,
            RowSpacing = 10,
        };
        for (var i = 0; i < items.Length; i++)
        {
            var tile = BuildTile(items[i].Icon, items[i].Label, p, items[i].Click);
            var row = i / 2;
            // 行数随条目数增长 (磁贴超过 4 个时不再受固定两行限制)
            while (grid.RowDefinitions.Count <= row) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            Grid.SetRow(tile, row);
            Grid.SetColumn(tile, i % 2);
            grid.Children.Add(tile);
        }
        return grid;
    }

    private static Button BuildTile(string iconName, string label, Palette p, Action click)
    {
        var image = new Image
        {
            Width = 22,
            Height = 22,
            Source = SvgIcons.Load(iconName, white: p.Dark),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var caption = new TextBlock
        {
            Text = label,
            FontSize = 12.5,
            Foreground = new SolidColorBrush(p.Dark ? Color.Parse("#DCE3EE") : Color.Parse("#2A3240")),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        var stack = new StackPanel
        {
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children = { image, caption },
        };
        var button = new Button
        {
            Content = stack,
            Padding = new Thickness(8, 14),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = p.HoverBrush,
            BorderThickness = new Thickness(1),
            BorderBrush = p.DividerBrush,
            CornerRadius = new CornerRadius(Shape.RadiusM),
        };
        button.Click += (_, _) => click();
        return button;
    }

    public static Button Flat(string text, Palette p, Action click, bool primary = false)
    {
        var button = new Button
        {
            Content = new TextBlock
            {
                Text = text,
                FontSize = 12.5,
                FontWeight = primary ? FontWeight.SemiBold : FontWeight.Normal,
                Foreground = primary ? new SolidColorBrush(p.Dark ? Colors.White : Colors.White) : p.AccentBrush,
            },
            Padding = new Thickness(14, 7),
            CornerRadius = new CornerRadius(Shape.RadiusS),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Background = primary ? p.AccentBrush : p.HoverBrush,
            BorderThickness = new Thickness(1),
            BorderBrush = primary ? p.AccentBrush : p.DividerBrush,
        };
        button.Click += (_, _) => click();
        return button;
    }

    public static Control Row(double gap, params Control[] items)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = gap };
        foreach (var it in items) sp.Children.Add(it);
        return sp;
    }

    public static Control Gap(double height) => new Border { Height = height, Background = Brushes.Transparent };

    public static Control Divider(Palette p) => new Border
    {
        Height = 1,
        Background = p.DividerBrush,
        Margin = new Thickness(0, 10, 0, 10),
    };

    /// <summary>空态（对应 v1.2 的 .usb-empty / .printer-empty）。</summary>
    public static Control EmptyState(string iconName, string title, string hint, Palette p)
    {
        var stack = new StackPanel
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 26, 0, 26),
            Children =
            {
                new Image
                {
                    Width = 30,
                    Height = 30,
                    Source = SvgIcons.Load(iconName, white: p.Dark),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Opacity = 0.45,
                },
                new TextBlock
                {
                    Text = title,
                    FontSize = 13,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = p.MutedBrush,
                },
                new TextBlock
                {
                    Text = hint,
                    FontSize = 11.5,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 260,
                    Foreground = p.MutedBrush,
                    Opacity = 0.8,
                },
            },
        };
        return stack;
    }

    /// <summary>图标（深/浅主题自适应）。</summary>
    public static Image Icon(string name, Palette p, double size = 20) => new()
    {
        Width = size,
        Height = size,
        Source = SvgIcons.Load(name, white: p.Dark),
        VerticalAlignment = VerticalAlignment.Center,
    };
}
