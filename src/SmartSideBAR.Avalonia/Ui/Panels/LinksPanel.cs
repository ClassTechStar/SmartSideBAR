// LinksPanel, 建议 #3/#5: 标签页 + 正方形卡片网格 (对标希沃网页快捷入口:
// DeepSeek/GLM 等网页版 AI 一键直达），支持内联新增。分类来自 link.category,
// 自定义值自动成页; 保存走 LinksService（读取过滤视图 / 全量回写语义见 Core）。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed class LinksPanel(SidebarServices s, Palette p, Action<LinkItem> open) : SidebarPanel(s, p)
{
    private static readonly string[] KnownTabs = ["全部", "应用", "网站", "工具", "AI"];

    private string _tab = "全部";
    private bool _adding;
    // P2-3: 分类切换时的入场转场（首次构建不动画）
    private string? _lastTab;
    private int _enterDirection = 1;

    protected override Control Build()
    {
        var links = Services.Config.Current.Links
            .Where(l => l.Enabled && !string.IsNullOrWhiteSpace(l.Url))
            .ToList();

        // 页签变化 → 记下方向，供 OnBuilt 播放入场动画
        _enterDirection = _lastTab is null ? 0
            : string.Compare(_tab, _lastTab, StringComparison.Ordinal) >= 0 ? 1 : -1;
        _lastTab = _tab;

        var root = Stack(8);
        root.Children.Add(BuildTabs(links));

        if (_adding)
        {
            root.Children.Add(BuildAddForm());
        }

        if (links.Count == 0 && !_adding)
        {
            root.Children.Add(PanelKit.EmptyState("link", "还没有快捷入口",
                "点击下方「＋ 添加」放入教学网站或网页版 AI（DeepSeek、GLM…），一键直达。", Palette));
        }
        else
        {
            root.Children.Add(BuildGrid(links));
        }
        return root;
    }

    /// <summary>P2-3: 分类切换后播放横向入场动画；关闭动效时直接显示。</summary>
    protected override void OnBuilt()
    {
        if (_enterDirection == 0 || Bounds.Width <= 0) return;
        var animate = !Services.Config.Current.Appearance.ReduceMotion;
        _ = PageTransition.SlideInAsync(this, Bounds.Width, _enterDirection, animate);
    }

    /// <summary>页签: 已知分类 ∪ 数据里出现过的自定义分类; 「全部」恒在首位。</summary>
    private Control BuildTabs(List<LinkItem> links)
    {
        var custom = links.Select(l => string.IsNullOrWhiteSpace(l.Category) ? "网站" : l.Category.Trim())
            .Where(c => !KnownTabs.Contains(c, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var tabs = new List<string> { "全部" };
        tabs.AddRange(KnownTabs.Skip(1));
        tabs.AddRange(custom);

        var row = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var t in tabs)
        {
            var tab = t;
            var btn = PanelKit.Flat(tab, Palette, () =>
            {
                if (_tab == tab) return;
                _tab = tab;
                Refresh();
            }, primary: string.Equals(_tab, tab, StringComparison.OrdinalIgnoreCase));
            btn.Padding = new Thickness(13, 5);
            row.Children.Add(btn);
        }
        return row;
    }

    /// <summary>正方形图标卡片网格（建议 #3: 3 列, 字标圆 + 名称, 视觉干净找得快）。</summary>
    private Control BuildGrid(List<LinkItem> links)
    {
        var visible = links.Where(l => _tab == "全部"
            || string.Equals((string.IsNullOrWhiteSpace(l.Category) ? "网站" : l.Category.Trim()), _tab, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var grid = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = 104, ItemHeight = 96 };
        foreach (var link in visible)
        {
            grid.Children.Add(BuildCard(link));
        }
        // 新增入口: 虚线卡片 (KoBar add-button 语义)
        var add = new Button
        {
            Width = 96,
            Height = 88,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(1.5),
            BorderBrush = Palette.DividerBrush,
            CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusM),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = new TextBlock
            {
                Text = "＋ 添加",
                FontSize = 12.5,
                Foreground = Palette.MutedBrush,
            },
        };
        add.Click += (_, _) =>
        {
            _adding = !_adding;
            Refresh();
        };
        grid.Children.Add(add);
        return grid;
    }

    private Control BuildCard(LinkItem link)
    {
        var letter = string.IsNullOrWhiteSpace(link.Name) ? "?" : link.Name.Trim()[0].ToString();
        var stack = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        stack.Children.Add(new Border
        {
            Width = 30,
            Height = 30,
            CornerRadius = new CornerRadius(15),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = Palette.AccentSoftBrush,
            Child = new TextBlock
            {
                Text = letter,
                FontSize = 14,
                FontWeight = FontWeight.SemiBold,
                Foreground = Palette.AccentBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        });
        stack.Children.Add(new TextBlock
        {
            Text = link.Name,
            FontSize = 11.5,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 30,
            Foreground = new SolidColorBrush(Palette.Dark ? Color.Parse("#DCE3EE") : Color.Parse("#2A3240")),
        });
        var btn = new Button
        {
            Width = 96,
            Height = 88,
            Padding = new Thickness(4),
            Content = stack,
            Background = Palette.HoverBrush,
            BorderThickness = new Thickness(1),
            BorderBrush = Palette.DividerBrush,
            CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusM),
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        btn.Click += (_, _) => open(link);
        return btn;
    }

    /// <summary>内联新增表单: 名称 + URL + 分类, 回车或点击添加即落盘。</summary>
    private Control BuildAddForm()
    {
        var name = new TextBox { Watermark = "名称（如: DeepSeek）" };
        var url = new TextBox { Watermark = "网址（https://…）" };
        var category = new ComboBox
        {
            ItemsSource = KnownTabs.Skip(1).ToArray(),
            SelectedIndex = 1,
            MinWidth = 110,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var add = PanelKit.Flat("添加", Palette, () => Submit(name, url, category), primary: true);
        var cancel = PanelKit.Flat("取消", Palette, () =>
        {
            _adding = false;
            Refresh();
        });
        url.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter) Submit(name, url, category);
        };

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var fields = Stack(6);
        fields.Children.Add(name);
        fields.Children.Add(url);
        grid.Children.Add(fields);
        var actions = PanelKit.Row(6, category, add, cancel);
        actions.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(actions, 1);
        grid.Children.Add(actions);

        return new Border
        {
            Background = new SolidColorBrush(Palette.Card),
            CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusM),
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            BorderBrush = Palette.AccentBrush,
            Child = grid,
        };
    }

    private void Submit(TextBox name, TextBox url, ComboBox category)
    {
        var n = name.Text?.Trim() ?? "";
        var u = url.Text?.Trim() ?? "";
        if (n.Length == 0 || u.Length == 0)
        {
            ToastService.Instance.Show("名称和网址都要填", "warn");
            return;
        }
        if (!u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            u = "https://" + u;
        }
        try
        {
            Services.Links.Add(n, u, category: (category.SelectedItem as string) ?? "网站");
            _adding = false;
            Refresh();
            ToastService.Instance.Show($"已添加「{n}」", "success");
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"添加失败: {ex.Message}", "error");
        }
    }
}
