// 常规页 VM: 课表文本/侧栏位置/触发方式/全屏自动收起/开机自启。
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

public sealed class GeneralSettingsViewModel(IShellService shell, ISettingsHost host) : SettingsPageViewModel
{
    public static readonly string[] SideOptions = ["left", "right"];
    public static readonly string[] TriggerOptions =
        ["常驻胶囊（触屏教学推荐）", "极细触发条·点击展开（防误触）", "极细触发条·悬停滑出"];

    private string _lessonText = "";
    private int _sideIndex = 1;
    private int _triggerIndex;
    private bool _autoCollapse;
    private bool _autoLaunch;
    private double _triggerWidth = 14;
    private double _hoverDelayMs = 150;
    private double _collapseDelaySec = 5;
    private double _railWidth = 72;
    private double _panelWidth = 380;
    private int _iconIndex = 1;
    private int _topmostIndex = 1;
    private bool _collapseOnFocusLoss;
    private bool _liquidGlass = true;

    /// <summary>XAML 绑定的数据源 (静态数组不能直接绑定)。</summary>
    public IReadOnlyList<string> SideOptionsSource => SideOptions;

    public IReadOnlyList<string> TriggerOptionsSource => TriggerOptions;

    public override string Title => "常规";

    public string LessonText { get => _lessonText; set => SetProperty(ref _lessonText, value); }

    public int SideIndex { get => _sideIndex; set => SetProperty(ref _sideIndex, value); }

    public int TriggerIndex { get => _triggerIndex; set => SetProperty(ref _triggerIndex, value); }

    public bool AutoCollapse { get => _autoCollapse; set => SetProperty(ref _autoCollapse, value); }

    public bool AutoLaunch { get => _autoLaunch; set => SetProperty(ref _autoLaunch, value); }

    public static readonly string[] IconSizeOptions = ["小 (24)", "中 (28)", "大 (32)"];
    public static readonly string[] TopmostOptions = ["从不置顶", "自动（推荐）", "强制置顶（可能遮挡弹窗）"];
    public static readonly double[] IconDipValues = [24, 28, 32];
    public IReadOnlyList<string> IconSizeOptionsSource => IconSizeOptions;
    public IReadOnlyList<string> TopmostOptionsSource => TopmostOptions;

    /// <summary>触发条宽度 (DIP)。</summary>
    public double TriggerWidth { get => _triggerWidth; set => SetProperty(ref _triggerWidth, value); }

    /// <summary>悬停展开延时 (ms)。</summary>
    public double HoverDelayMs { get => _hoverDelayMs; set => SetProperty(ref _hoverDelayMs, value); }

    /// <summary>移出收起延时 (秒)。</summary>
    public double CollapseDelaySec { get => _collapseDelaySec; set => SetProperty(ref _collapseDelaySec, value); }

    /// <summary>图标轨宽度 (DIP)。</summary>
    public double RailWidth { get => _railWidth; set => SetProperty(ref _railWidth, value); }

    /// <summary>面板宽度 (DIP)。</summary>
    public double PanelWidth { get => _panelWidth; set => SetProperty(ref _panelWidth, value); }

    /// <summary>图标尺寸档位索引 (24/28/32)。</summary>
    public int IconIndex { get => _iconIndex; set => SetProperty(ref _iconIndex, value); }

    /// <summary>置顶三档索引。</summary>
    public int TopmostIndex { get => _topmostIndex; set => SetProperty(ref _topmostIndex, value); }

    public bool CollapseOnFocusLoss { get => _collapseOnFocusLoss; set => SetProperty(ref _collapseOnFocusLoss, value); }

    public bool LiquidGlass { get => _liquidGlass; set => SetProperty(ref _liquidGlass, value); }

    public override void LoadFrom(SidekickConfig config)
    {
        LessonText = LessonPeriodText.Format(config.Lesson.Periods);
        SideIndex = config.Display.SidebarSide == SidebarSide.Left ? 0 : 1;
        TriggerIndex = config.Display.RailTrigger switch
        {
            "edge" => 1,
            "edge-hover" => 2,
            _ => 0,
        };
        AutoCollapse = config.Display.AutoCollapseOnFullscreen;
        AutoLaunch = shell.GetAutoLaunch();
        TriggerWidth = config.Display.TriggerWidthDip;
        HoverDelayMs = config.Display.HoverExpandDelayMs;
        CollapseDelaySec = config.Display.CollapseDelaySec;
        RailWidth = config.Display.RailWidthDip;
        PanelWidth = config.Display.PanelWidthDip;
        IconIndex = ClosestIconIndex(config.Display.IconDip);
        TopmostIndex = config.Display.TopmostPolicy switch
        {
            "never" => 0,
            "always" => 2,
            _ => 1,
        };
        CollapseOnFocusLoss = config.Display.CollapseOnFocusLoss;
        LiquidGlass = config.Appearance.LiquidGlass;
    }

    public override IEnumerable<ConfigChange> CollectChanges()
    {
        var changes = new List<ConfigChange>
        {
            ConfigChange.From("display.sidebarSide", SideIndex == 0 ? "left" : "right"),
            ConfigChange.From("display.railTrigger", TriggerIndex switch
            {
                1 => "edge",
                2 => "edge-hover",
                _ => "always",
            }),
            ConfigChange.From("display.autoCollapseOnFullscreen", AutoCollapse),
        };

        changes.Add(ConfigChange.From("display.triggerWidthDip", TriggerWidth));
        changes.Add(ConfigChange.From("display.hoverExpandDelayMs", (int)HoverDelayMs));
        changes.Add(ConfigChange.From("display.collapseDelaySec", CollapseDelaySec));
        changes.Add(ConfigChange.From("display.railWidthDip", RailWidth));
        changes.Add(ConfigChange.From("display.panelWidthDip", PanelWidth));
        changes.Add(ConfigChange.From("display.iconDip", IconDipValues[Math.Clamp(IconIndex, 0, IconDipValues.Length - 1)]));
        changes.Add(ConfigChange.From("display.topmostPolicy", TopmostIndex switch
        {
            0 => "never",
            2 => "always",
            _ => "normal",
        }));
        changes.Add(ConfigChange.From("display.collapseOnFocusLoss", CollapseOnFocusLoss));
        changes.Add(ConfigChange.From("appearance.liquidGlass", LiquidGlass));

        var periods = LessonPeriodText.Parse(LessonText);
        if (string.IsNullOrWhiteSpace(LessonText) || periods.Count > 0)
        {
            changes.Add(ConfigChange.From("lesson.periods", periods));
        }
        else
        {
            host.Toast("课表格式无法解析 (示例: 8:00-8:45 语文; 8:55-9:40 数学), 本次未保存课表", "warn");
        }
        return changes;
    }

    private static int ClosestIconIndex(double dip)
    {
        var best = 0;
        var bestDelta = double.MaxValue;
        for (var i = 0; i < IconDipValues.Length; i++)
        {
            var delta = Math.Abs(IconDipValues[i] - dip);
            if (delta < bestDelta) { bestDelta = delta; best = i; }
        }
        return best;
    }

    /// <summary>开机自启是系统态而非配置项, 保存后由宿主应用。</summary>
    public void ApplyShellSettings() => shell.SetAutoLaunch(AutoLaunch);
}
