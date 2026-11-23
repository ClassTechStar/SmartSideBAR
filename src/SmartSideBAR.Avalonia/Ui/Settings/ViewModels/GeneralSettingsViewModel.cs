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

    /// <summary>XAML 绑定的数据源 (静态数组不能直接绑定)。</summary>
    public IReadOnlyList<string> SideOptionsSource => SideOptions;

    public IReadOnlyList<string> TriggerOptionsSource => TriggerOptions;

    public override string Title => "常规";

    public string LessonText { get => _lessonText; set => SetProperty(ref _lessonText, value); }

    public int SideIndex { get => _sideIndex; set => SetProperty(ref _sideIndex, value); }

    public int TriggerIndex { get => _triggerIndex; set => SetProperty(ref _triggerIndex, value); }

    public bool AutoCollapse { get => _autoCollapse; set => SetProperty(ref _autoCollapse, value); }

    public bool AutoLaunch { get => _autoLaunch; set => SetProperty(ref _autoLaunch, value); }

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

    /// <summary>开机自启是系统态而非配置项, 保存后由宿主应用。</summary>
    public void ApplyShellSettings() => shell.SetAutoLaunch(AutoLaunch);
}
