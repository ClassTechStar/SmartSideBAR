// 快捷键页 VM: 五个全局热键文本。
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

public sealed class HotkeysSettingsViewModel : SettingsPageViewModel
{
    private string _capture = "";
    private string _annotate = "";
    private string _longshot = "";
    private string _floatBall = "";
    private string _ime = "";
    private string _sidebar = "";

    public override string Title => "快捷键";

    public string Capture { get => _capture; set => SetProperty(ref _capture, value); }

    public string Annotate { get => _annotate; set => SetProperty(ref _annotate, value); }

    public string Longshot { get => _longshot; set => SetProperty(ref _longshot, value); }

    public string FloatBall { get => _floatBall; set => SetProperty(ref _floatBall, value); }

    public string Ime { get => _ime; set => SetProperty(ref _ime, value); }

    /// <summary>呼出/隐藏侧栏 (空=不注册)。</summary>
    public string Sidebar { get => _sidebar; set => SetProperty(ref _sidebar, value); }

    public override void LoadFrom(SidekickConfig config)
    {
        Capture = config.Capture.Hotkey;
        Annotate = config.Capture.AnnotateHotkey;
        Longshot = config.Capture.LongshotHotkey;
        FloatBall = config.FloatBall.Hotkey;
        Ime = string.IsNullOrWhiteSpace(config.Ime.Hotkey) ? "Ctrl+Shift+I" : config.Ime.Hotkey;
        Sidebar = config.Display.SidebarHotkey;
    }

    public override IEnumerable<ConfigChange> CollectChanges() =>
    [
        ConfigChange.From("capture.hotkey", Capture?.Trim()),
        ConfigChange.From("capture.annotateHotkey", Annotate?.Trim()),
        ConfigChange.From("capture.longshotHotkey", Longshot?.Trim()),
        ConfigChange.From("floatBall.hotkey", FloatBall?.Trim()),
        ConfigChange.From("ime.hotkey", string.IsNullOrWhiteSpace(Ime) ? "Ctrl+Shift+I" : Ime.Trim()),
        ConfigChange.From("display.sidebarHotkey", string.IsNullOrWhiteSpace(Sidebar) ? "" : Sidebar.Trim()),
    ];
}
