// 设置窗 (M2): 导航 + 页宿主 + 保存条全部在 axaml; 本文件只做页面装配、宿主能力与保存落盘。
// 保存语义与改造前一致: 点「保存」→ 批量 ApplyMany → Toast → 关闭。
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using SmartSideBAR.Avalonia.Ui.Settings.Pages;
using SmartSideBAR.Avalonia.Ui.Settings.ViewModels;
using SmartSideBAR.Avalonia.Updater;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia.Ui.Settings;

public sealed partial class SettingsWindow : Window, ISettingsHost
{
    private readonly ConfigService _config;
    private readonly AutomationPage _automationPage = new();
    private readonly Control[] _pageViews;

    public SettingsWindow(ConfigService config, IShellService shell, HotkeyService hotkeys, IDiagnosticsService diag)
    {
        InitializeComponent();
        _config = config;

        var palette = AppTheme.ForSettings(config.Current.Appearance);
        RequestedThemeVariant = palette.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        Background = new SolidColorBrush(palette.Inset);
        ThemeResources.Apply(Resources, palette);

        var appearance = new AppearanceSettingsViewModel();
        var general = new GeneralSettingsViewModel(shell, this);
        var capture = new CaptureSettingsViewModel();
        var recorder = new RecorderSettingsViewModel();
        var devices = new DevicesSettingsViewModel();
        var hotkeysVm = new HotkeysSettingsViewModel();
        var diagnostics = new DiagnosticsSettingsViewModel(config, diag, this);
        var about = new AboutSettingsViewModel(config, this);

        var pages = new SettingsPageViewModel[]
        {
            appearance, general, capture, recorder, devices, hotkeysVm, diagnostics, about,
        };
        var titles = new List<string> { "外观", "常规", "自动化", "截图", "录屏", "设备", "快捷键", "诊断" };
        var views = new List<Control>
        {
            new AppearancePage(appearance),
            new GeneralPage(general),
            _automationPage,
            new CapturePage(capture),
            new RecorderPage(recorder),
            new DevicesPage(devices),
            new HotkeysPage(hotkeysVm),
            new DiagnosticsPage(diagnostics),
        };
#if DEBUG
        // 设计规范页是开发脚手架, 正式包不出现
        titles.Add("设计规范");
        views.Add(DesignSpecPage.Build(palette));
#endif
        titles.Add("关于");
        views.Add(new AboutPage(about));

        Vm = new SettingsWindowViewModel(titles, pages);
        DataContext = Vm;
        _pageViews = [.. views];

        Vm.LoadFrom(config.Current);
        ((AppearancePage)_pageViews[0]).ApplyPalette(palette);
        _automationPage.LoadRules(config.Current.Automation.Rules);

        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SettingsWindowViewModel.SelectedIndex)) SyncPage();
        };
        Vm.SaveRequested += Save;
        SyncPage();
    }

    public SettingsWindowViewModel Vm { get; }

    private void SyncPage()
    {
        var index = Math.Clamp(Vm.SelectedIndex, 0, _pageViews.Length - 1);
        PageHost.Content = _pageViews[index];
    }

    private void Save()
    {
        var changes = Vm.CollectChanges().ToList();
        // 自动化编辑器仍为视图内编辑 (M2 过渡): 规则由页面收集后并入同一批写盘
        changes.Add(ConfigChange.From("automation.rules", _automationPage.CollectRules()));
        _ = _config.ApplyMany(changes);
        if (Vm.Pages.OfType<GeneralSettingsViewModel>().FirstOrDefault() is { } general)
        {
            general.ApplyShellSettings();
        }
        ToastService.Instance.Show("设置已保存", "success");
        Close();
    }

    // ---------------------------------------------------------------- ISettingsHost

    public async Task<string?> PickSaveJsonAsync(string suggestedFileName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "导出配置",
            SuggestedFileName = suggestedFileName,
            DefaultExtension = "json",
            FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
        });
        return file?.Path.LocalPath;
    }

    public async Task<string?> PickOpenJsonAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "导入配置",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
        });
        return files.Count == 0 ? null : files[0].Path.LocalPath;
    }

    public void Toast(string message, string level) => ToastService.Instance.Show(message, level);

    public void InstallAndExit(string installerPath)
    {
        if (!UpdateService.LaunchInstaller(installerPath))
        {
            Toast("安装包丢失，请重新检查更新", "error");
            return;
        }
        _ = Task.Run(async () =>
        {
            await Task.Delay(900); // 给安装器启动时间, 然后退出释放文件锁
            Environment.Exit(0);
        });
    }
}
