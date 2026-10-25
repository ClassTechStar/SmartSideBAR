// 快捷设置面板 —— Seelen quick-settings 语法: 2 列 80px 大磁贴开关 + 工作区切换
// (KoBar 工作区)。rail 的 settings 图标直达这里;「全部设置」再进完整 SettingsWindow。
// 磁贴状态即时落盘; 主题相关项切换后即时 ApplyTheme。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed class QuickSettingsPanel(SidebarServices s, Palette p) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var cfg = Services.Config.Current;
        var root = Stack(12);

        root.Children.Add(Card("工作区", BuildWorkspaceTiles(cfg)));
        root.Children.Add(Card("快捷开关", BuildToggleTiles(cfg)));

        var more = PanelKit.Flat("全部设置…", Palette, () => Services.Dispatch("settings"), primary: true);
        more.HorizontalAlignment = HorizontalAlignment.Stretch;
        more.HorizontalContentAlignment = HorizontalAlignment.Center;
        root.Children.Add(more);
        root.Children.Add(PanelKit.Hint("热键、保存目录、打印机轮询等高级选项在完整设置中。", Palette));
        return root;
    }

    /// <summary>KoBar Surface 卡片容器（与设置窗同语法）。</summary>
    private Control Card(string title, Control content)
    {
        var stack = new StackPanel { Spacing = 8 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = Palette.MutedBrush,
        });
        stack.Children.Add(content);
        return new Border
        {
            Background = new SolidColorBrush(Palette.Card),
            CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusM),
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            BorderBrush = Palette.DividerBrush,
            BoxShadow = BoxShadows.Parse(DesignTokens.Current.Shadow.E1),
            Child = stack,
        };
    }

    /// <summary>KoBar 工作区: 每个预设一枚磁贴, 当前项 accent 实心。</summary>
    private Control BuildWorkspaceTiles(SidekickConfig cfg)
    {
        var presets = cfg.Workspace.Presets;
        if (presets.Count == 0) return PanelKit.Hint("尚无工作区预设。", Palette);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10, RowSpacing = 10 };
        for (var i = 0; i < presets.Count; i++)
        {
            var preset = presets[i];
            var active = string.Equals(preset.Name, cfg.Workspace.Current, StringComparison.OrdinalIgnoreCase);
            var tile = new Button
            {
                MinHeight = 52,
                CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusM),
                Padding = new Thickness(8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = active ? Palette.AccentBrush : Palette.HoverBrush,
                BorderThickness = new Thickness(1),
                BorderBrush = active ? Palette.AccentBrush : Palette.DividerBrush,
                Content = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center },
            };
            var stack = (StackPanel)tile.Content!;
            stack.Children.Add(new TextBlock
            {
                Text = preset.Name,
                FontSize = 13,
                FontWeight = FontWeight.SemiBold,
                Foreground = active ? Brushes.White : new SolidColorBrush(Palette.Dark ? Color.Parse("#E6EBF3") : Color.Parse("#1B2230")),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            stack.Children.Add(new TextBlock
            {
                Text = active ? "使用中" : $"{preset.Modules.Count} 个模块",
                FontSize = 10.5,
                Foreground = active ? new SolidColorBrush(Color.FromArgb(0xD9, 0xFF, 0xFF, 0xFF)) : Palette.MutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            var name = preset.Name;
            tile.Click += (_, _) =>
            {
                if (!active) Services.Dispatch($"workspace:{name}");
            };
            Grid.SetRow(tile, i / 2);
            Grid.SetColumn(tile, i % 2);
            grid.Children.Add(tile);
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        return grid;
    }

    /// <summary>Seelen quick-settings: 2 列 min-height 80px 大磁贴, 选中 = accent 实心。</summary>
    private Control BuildToggleTiles(SidekickConfig cfg)
    {
        (string Label, bool On, Action<string> Apply)[] toggles =
        [
            ("液态玻璃", cfg.Appearance.LiquidGlass,
                v => Services.Config.Set("appearance.liquidGlass", v == "1")),
            ("减弱动效", cfg.Appearance.ReduceMotion,
                v => Services.Config.Set("appearance.reduceMotion", v == "1")),
            ("全屏自动收起", cfg.Display.AutoCollapseOnFullscreen,
                v => Services.Config.Set("display.autoCollapseOnFullscreen", v == "1")),
            ("跟随系统色", string.Equals(cfg.Appearance.Accent, "auto", StringComparison.OrdinalIgnoreCase),
                v => Services.Config.Set("appearance.accent", v)),
            ("USB 监控", cfg.Usb.Enabled,
                v => Services.Config.Set("usb.enabled", v == "1")),
            ("录屏", cfg.Recorder.Enabled,
                v => Services.Config.Set("recorder.enabled", v == "1")),
        ];

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), ColumnSpacing = 10, RowSpacing = 10 };
        for (var i = 0; i < toggles.Length; i++)
        {
            var (label, on, apply) = toggles[i];
            var tile = new Button
            {
                MinHeight = 80,
                CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusM),
                Padding = new Thickness(8, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Background = on ? Palette.AccentBrush : Palette.HoverBrush,
                BorderThickness = new Thickness(1),
                BorderBrush = on ? Palette.AccentBrush : Palette.DividerBrush,
            };
            var stack = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            // 状态图标: 开 = 对勾, 关 = 空心圈 (触控远距离可辨)
            stack.Children.Add(new TextBlock
            {
                Text = on ? "✓" : "○",
                FontSize = 20,
                FontWeight = FontWeight.Bold,
                Foreground = on ? Brushes.White : Palette.MutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            stack.Children.Add(new TextBlock
            {
                Text = label,
                FontSize = 12.5,
                FontWeight = FontWeight.Medium,
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Center,
                Foreground = on ? Brushes.White : new SolidColorBrush(Palette.Dark ? Color.Parse("#DCE3EE") : Color.Parse("#2A3240")),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            tile.Content = stack;
            tile.Click += (_, _) =>
            {
                apply(on ? "0" : "1");
                Refresh();
                (TopLevel.GetTopLevel(this) as Views.SidebarWindow)?.ApplyTheme();
            };
            Grid.SetRow(tile, i / 2);
            Grid.SetColumn(tile, i % 2);
            grid.Children.Add(tile);
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }
        return grid;
    }
}
