// M3: 设置面板 VM（主题 / 开机自启 / 直达完整设置）。
using SmartSideBAR.Avalonia.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Panels.ViewModels;

internal sealed class SettingsPanelViewModel : PanelViewModel
{
    private bool _loading;
    private int _themeIndex;
    private bool _autoLaunch;

    public SettingsPanelViewModel(SidebarServices services, Palette palette) : base(services, palette)
    {
        OpenSettingsCommand = new RelayCommand(_ => Services.Dispatch("settings"));
    }

    public string[] Themes { get; } = ["浅色（默认）", "深色"];

    public int ThemeIndex
    {
        get => _themeIndex;
        set
        {
            if (!SetProperty(ref _themeIndex, value) || _loading) return;
            Services.Config.Set("appearance.theme", value == 1 ? "dark" : "light");
            // 面板派发事件中不能替换可视树: 交给窗口延后重建面板并套用主题
            RequestThemeApply();
        }
    }

    public bool AutoLaunch
    {
        get => _autoLaunch;
        set
        {
            if (!SetProperty(ref _autoLaunch, value) || _loading) return;
            try
            {
                Services.Shell.SetAutoLaunch(value);
            }
            catch (Exception ex)
            {
                ToastService.Instance.Show($"设置自启动失败: {ex.Message}", "error");
            }
        }
    }

    public RelayCommand OpenSettingsCommand { get; }

    public string AdvancedHint { get; } =
        "热键、截图格式、录屏帧率、侧栏停靠边等高级选项在完整设置中。";

    public override void Reload()
    {
        _loading = true;
        try
        {
            ThemeIndex = string.Equals(
                Services.Config.Current.Appearance.Theme, "dark", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            AutoLaunch = Services.Shell.GetAutoLaunch();
        }
        finally
        {
            _loading = false;
        }
    }
}
