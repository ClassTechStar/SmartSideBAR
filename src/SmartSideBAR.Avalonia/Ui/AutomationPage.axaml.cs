// 自动化设置页代码后置: 规则卡片动态构建/收集 + 扩展状态展示。
// 行布局仍走 KoBar「标签在左 / 控件在右」语法; 规则行由 LoadRules/CollectRules 与
// ConfigService 的 automation.rules 白名单键对接 (P2-6 Ruleset 消费)。
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Design;
using SmartSideBAR.Core.Extensions;
using SmartSideBAR.Core.Rules;

namespace SmartSideBAR.Avalonia.Ui;

public sealed partial class AutomationPage : UserControl
{
    // 条件类型/匹配方式下拉与枚举声明顺序一一对应 (index ↔ enum value)
    private static readonly RuleConditionKind[] Kinds =
        [RuleConditionKind.LessonState, RuleConditionKind.Fullscreen, RuleConditionKind.HourOfDay, RuleConditionKind.WeekDay];
    private static readonly RuleMatchMode[] Modes =
        [RuleMatchMode.Equals, RuleMatchMode.NotEquals, RuleMatchMode.AtLeast, RuleMatchMode.AtMost];
    private static readonly string[] KindNames = ["课堂状态", "全屏", "小时 (0-23)", "星期 (0=周日)"];
    private static readonly string[] ModeNames = ["等于", "不等于", "大于等于", "小于等于"];

    private readonly List<RuleCard> _cards = [];
    private Palette _p = AppTheme.From(null);

    private sealed class ConditionRow
    {
        public required ComboBox Kind { get; init; }
        public required ComboBox Mode { get; init; }
        public required TextBox Value { get; init; }
    }

    private sealed class RuleCard
    {
        public required CheckBox Enabled { get; init; }
        public required TextBox Action { get; init; }
        public required ComboBox Match { get; init; }
        public required StackPanel ConditionPanel { get; init; }
        public required List<ConditionRow> Rows { get; init; }
        public required Border Root { get; init; }
    }

    public AutomationPage()
    {
        InitializeComponent();
        RulesCard.CornerRadius = ExtCard.CornerRadius =
            new CornerRadius(DesignTokens.Current.Shape.RadiusM);
        KeyboardNavigation.SetTabNavigation(RulesHost, KeyboardNavigationMode.Continue);
        AddRuleBtn.Click += (_, _) => AddRuleCard(new AutomationRuleConfig
        {
            Conditions = [new AutomationConditionConfig { Kind = RuleConditionKind.LessonState, Mode = RuleMatchMode.Equals, Value = "OnClass" }],
        });
        // 独立构造（测试/设计器）时先套一份默认调色板，宿主随后会覆盖
        ApplyPalette(AppTheme.From(null));
    }

    /// <summary>载入规则并刷新扩展状态 (设置窗 LoadFrom 时调用)。</summary>
    public void LoadRules(IEnumerable<AutomationRuleConfig> rules)
    {
        RulesHost.Children.Clear();
        _cards.Clear();
        foreach (var rule in rules) AddRuleCard(rule);
        RefreshExtensionStatus();
    }

    /// <summary>把编辑器当前内容收集为持久化规则列表 (设置窗 Save 时调用)。</summary>
    public List<AutomationRuleConfig> CollectRules() =>
        _cards.Select(card => new AutomationRuleConfig
        {
            Enabled = card.Enabled.IsChecked == true,
            ActionId = card.Action.Text?.Trim() ?? "",
            AllMustMatch = card.Match.SelectedIndex != 1,
            Conditions = card.Rows.Select(r => new AutomationConditionConfig
            {
                Kind = Kinds[Math.Clamp(r.Kind.SelectedIndex, 0, Kinds.Length - 1)],
                Mode = Modes[Math.Clamp(r.Mode.SelectedIndex, 0, Modes.Length - 1)],
                Value = r.Value.Text?.Trim() ?? "",
            }).ToList(),
        }).ToList();

    /// <summary>主题/强调色变化时重刷页面资源; 动态规则卡仍按 Palette 直接着色。</summary>
    public void ApplyPalette(Palette p)
    {
        _p = p;
        ThemeResources.Apply(Resources, p);
        foreach (var card in _cards) ApplyCardPalette(card);
    }

    private void ApplyCardPalette(RuleCard card)
    {
        card.Root.Background = new SolidColorBrush(_p.Card);
        card.Root.BorderBrush = _p.DividerBrush;
    }

    // ------------------------------------------------------------ 规则卡片

    private void AddRuleCard(AutomationRuleConfig rule)
    {
        var enabled = new CheckBox
        {
            Content = "启用",
            IsChecked = rule.Enabled,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 52,
        };
        AutomationProperties.SetName(enabled, "启用此规则");

        var action = new TextBox
        {
            Text = rule.ActionId,
            Watermark = "动作 id，如 workspace:上课 / ime / dock",
            MinWidth = 230,
            Height = 30,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(action, "规则动作 id");

        var match = new ComboBox
        {
            ItemsSource = new[] { "全部条件满足", "任一条件满足" },
            SelectedIndex = rule.AllMustMatch ? 0 : 1,
            MinWidth = 120,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(match, "条件匹配方式");

        var remove = new Button { Content = "删除规则", VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(remove, "删除此规则");

        var rows = new List<ConditionRow>();
        var conditions = new StackPanel { Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        foreach (var c in rule.Conditions)
            conditions.Children.Add(BuildConditionRow(rows, conditions, c));

        var addCond = new Button { Content = "添加条件", HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(addCond, "为此规则添加条件");
        addCond.Click += (_, _) => conditions.Children.Add(BuildConditionRow(rows, conditions,
            new AutomationConditionConfig { Kind = RuleConditionKind.LessonState, Mode = RuleMatchMode.Equals, Value = "OnClass" }));

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 2) };
        AddCell(header, enabled, 0);
        AddCell(header, action, 1, new Thickness(8, 0));
        AddCell(header, match, 2, new Thickness(8, 0));
        AddCell(header, remove, 3);

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(header);
        stack.Children.Add(conditions);
        stack.Children.Add(addCond);

        var root = new Border
        {
            CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusS),
            Padding = new Thickness(12),
            BorderThickness = new Thickness(1),
            Child = stack,
        };
        var card = new RuleCard
        {
            Enabled = enabled,
            Action = action,
            Match = match,
            ConditionPanel = conditions,
            Rows = rows,
            Root = root,
        };
        remove.Click += (_, _) =>
        {
            RulesHost.Children.Remove(root);
            _cards.Remove(card);
        };

        _cards.Add(card);
        RulesHost.Children.Add(root);
        ApplyCardPalette(card);
    }

    private Border BuildConditionRow(List<ConditionRow> rows, StackPanel panel, AutomationConditionConfig condition)
    {
        var kind = new ComboBox
        {
            ItemsSource = KindNames,
            SelectedIndex = Math.Clamp((int)condition.Kind, 0, Kinds.Length - 1),
            MinWidth = 110,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var mode = new ComboBox
        {
            ItemsSource = ModeNames,
            SelectedIndex = Math.Clamp((int)condition.Mode, 0, Modes.Length - 1),
            MinWidth = 92,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var value = new TextBox
        {
            Text = condition.Value,
            Height = 30,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(kind, "条件类型");
        AutomationProperties.SetName(mode, "条件匹配方式");
        AutomationProperties.SetName(value, "条件取值");
        void RefreshWatermark() => value.Watermark = ValueWatermark(kind.SelectedIndex);
        RefreshWatermark();
        kind.SelectionChanged += (_, _) => RefreshWatermark();

        var del = new Button { Content = "×", Width = 28, VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(del, "删除此条件");

        var row = new ConditionRow { Kind = kind, Mode = mode, Value = value };
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto") };
        AddCell(grid, kind, 0);
        AddCell(grid, mode, 1, new Thickness(6, 0));
        AddCell(grid, value, 2, new Thickness(6, 0));
        AddCell(grid, del, 3, new Thickness(6, 0, 0, 0));

        rows.Add(row);
        del.Click += (_, _) =>
        {
            panel.Children.Remove(grid);
            rows.Remove(row);
        };
        return new Border { Child = grid, Padding = new Thickness(0, 2) };
    }

    private static void AddCell(Grid grid, Control child, int column, Thickness? margin = null)
    {
        if (margin is { } m) child.Margin = m;
        Grid.SetColumn(child, column);
        grid.Children.Add(child);
    }

    private static string ValueWatermark(int kindIndex) => kindIndex switch
    {
        0 => "如 OnClass（Idle/Prepare/OnClass/Breaking/AfterSchool）",
        1 => "True 或 False",
        2 => "0-23",
        _ => "0-6（0=周日）",
    };

    // ------------------------------------------------------------ 扩展状态

    private void RefreshExtensionStatus()
    {
        var reg = ExtensionHost.Registry;
        ExtApiText.Text = $"宿主 API 版本: {ExtensionRegistry.CurrentApiVersion}";
        var loaded = reg.Loaded;
        ExtLoadedText.Text = loaded.Count == 0
            ? "已加载扩展: 暂无"
            : "已加载扩展: " + string.Join("、", loaded.Select(e =>
                e.Version is null ? $"{e.Name}({e.Id})" : $"{e.Name}({e.Id} {e.Version})"));
        var failures = reg.Failures;
        ExtFailuresText.Text = failures.Count == 0
            ? "加载失败: 无"
            : "加载失败: " + string.Join("、", failures.Select(f => $"{f.DisplayName}: {f.Reason}"));
    }
}
