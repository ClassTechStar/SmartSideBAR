// M3: 快捷设置面板 VM（课堂统计 / 工作区 / 亮度音量 / 开关磁贴）。
// 磁贴状态即时落盘; 主题相关项切换后请求窗口 ApplyTheme（由面板 code-behind 延后执行）。
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Threading;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Diagnostics;
using SmartSideBAR.Windows.Audio;
using SmartSideBAR.Windows.Devices;

namespace SmartSideBAR.Avalonia.Ui.Panels.ViewModels;

internal sealed record WorkspaceTile(string Name, bool IsActive, string Subtitle, ICommand Command);

internal sealed record ToggleTile(string Label, bool On, ICommand Command);

internal sealed class QuickSettingsPanelViewModel : PanelViewModel
{
    private readonly BrightnessService _brightness = new();
    private readonly VolumeService _volume = new();
    private readonly DebounceGate _brightnessGate = new(450);
    private readonly DebounceGate _volumeGate = new(450);
    private readonly DispatcherTimer _brightnessPoll = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly DispatcherTimer _volumePoll = new() { Interval = TimeSpan.FromMilliseconds(50) };

    private double _brightnessValue = 50;
    private double _volumeValue = 50;
    private string _lessonStatsText = "";
    private bool _workspacesEmpty;

    public QuickSettingsPanelViewModel(SidebarServices services, Palette palette) : base(services, palette)
    {
        BrightnessVisible = _brightness.IsSupported;
        VolumeVisible = _volume.IsSupported;

        // 借鉴 SidebarDiagnostics 首次向导的防抖重定位: 拖动中只刷新标签, 停手 450ms 才真正下发。
        _brightnessPoll.Tick += (_, _) =>
        {
            if (!_brightnessGate.TryFire(Environment.TickCount64)) return;
            _brightnessPoll.Stop();
            _brightness.Set((int)Math.Round(BrightnessValue));
        };
        _volumePoll.Tick += (_, _) =>
        {
            if (!_volumeGate.TryFire(Environment.TickCount64)) return;
            _volumePoll.Stop();
            _volume.Set((int)Math.Round(VolumeValue));
        };

        PackCommand = new RelayCommand(_ => PackLesson());
        MoreSettingsCommand = new RelayCommand(_ => Services.Dispatch("settings"));
    }

    public ObservableCollection<WorkspaceTile> Workspaces { get; } = [];

    public ObservableCollection<ToggleTile> Toggles { get; } = [];

    public string LessonStatsText
    {
        get => _lessonStatsText;
        private set => SetProperty(ref _lessonStatsText, value);
    }

    public bool WorkspacesEmpty
    {
        get => _workspacesEmpty;
        private set => SetProperty(ref _workspacesEmpty, value);
    }

    public bool BrightnessVisible { get; }

    public bool VolumeVisible { get; }

    public bool HasClassroom => BrightnessVisible || VolumeVisible;

    public double BrightnessValue
    {
        get => _brightnessValue;
        set
        {
            if (!SetProperty(ref _brightnessValue, value)) return;
            Raise(nameof(BrightnessText));
            _brightnessGate.Trigger(Environment.TickCount64);
            if (!_brightnessPoll.IsEnabled) _brightnessPoll.Start();
        }
    }

    public string BrightnessText => $"屏幕亮度  {(int)Math.Round(BrightnessValue)}%";

    public double VolumeValue
    {
        get => _volumeValue;
        set
        {
            if (!SetProperty(ref _volumeValue, value)) return;
            Raise(nameof(VolumeText));
            _volumeGate.Trigger(Environment.TickCount64);
            if (!_volumePoll.IsEnabled) _volumePoll.Start();
        }
    }

    public string VolumeText => $"系统音量  {(int)Math.Round(VolumeValue)}%";

    public RelayCommand PackCommand { get; }

    public RelayCommand MoreSettingsCommand { get; }

    public override void Reload()
    {
        var cfg = Services.Config.Current;
        LessonStatsText = BuildLessonStats();
        BuildWorkspaces(cfg);
        BuildToggles(cfg);

        if (BrightnessVisible) BrightnessValue = _brightness.Get() ?? 50;
        if (VolumeVisible) VolumeValue = _volume.Get() ?? 50;
    }

    /// <summary>课堂统计卡: 单节看今值, 括号附累计。</summary>
    private string BuildLessonStats()
    {
        var snap = Services.Stats.Snapshot();
        double Of(IReadOnlyDictionary<string, double> d, string k) => d.TryGetValue(k, out var v) ? v : 0;
        string Cell(string label, string key)
        {
            var today = Of(snap.Today, key);
            var total = Of(snap.Totals, key);
            return $"{label} {today:0} (累计 {total:0})";
        }
        return string.Join('\n',
            Cell("批注笔迹", "annotate.stroke"),
            Cell("转文字", "annotate.ocr"),
            Cell("截图", "capture"),
            Cell("提醒", "reminder"));
    }

    /// <summary>KoBar 工作区: 每个预设一枚磁贴, 当前项 accent 实心。</summary>
    private void BuildWorkspaces(SidekickConfig cfg)
    {
        Workspaces.Clear();
        foreach (var preset in cfg.Workspace.Presets)
        {
            var name = preset.Name;
            var active = string.Equals(name, cfg.Workspace.Current, StringComparison.OrdinalIgnoreCase);
            Workspaces.Add(new WorkspaceTile(
                name,
                active,
                active ? "使用中" : $"{preset.Modules.Count} 个模块",
                new RelayCommand(_ =>
                {
                    if (!active) Services.Dispatch($"workspace:{name}");
                })));
        }
        WorkspacesEmpty = Workspaces.Count == 0;
    }

    private void BuildToggles(SidekickConfig cfg)
    {
        Toggles.Clear();
        AddToggle("液态玻璃", cfg.Appearance.LiquidGlass,
            v => Services.Config.Set("appearance.liquidGlass", v == "1"));
        AddToggle("减弱动效", cfg.Appearance.ReduceMotion,
            v => Services.Config.Set("appearance.reduceMotion", v == "1"));
        AddToggle("全屏自动收起", cfg.Display.AutoCollapseOnFullscreen,
            v => Services.Config.Set("display.autoCollapseOnFullscreen", v == "1"));
        // 注意: apply 收到的是 "0"/"1" 开关值, 必须映射成真正的 accent 值。
        AddToggle("跟随系统色", string.Equals(cfg.Appearance.Accent, "auto", StringComparison.OrdinalIgnoreCase),
            v => Services.Config.Set("appearance.accent", v == "1" ? "auto" : "#2B6EE0"));
        AddToggle("USB 监控", cfg.Usb.Enabled,
            v => Services.Config.Set("usb.enabled", v == "1"));
        AddToggle("录屏", cfg.Recorder.Enabled,
            v => Services.Config.Set("recorder.enabled", v == "1"));
    }

    private void AddToggle(string label, bool on, Action<string> apply) =>
        Toggles.Add(new ToggleTile(label, on, new RelayCommand(_ =>
        {
            apply(on ? "0" : "1");
            RequestRefresh();
            // ApplyTheme 会同步替换 PanelBody.Content —— 在点击事件派发中销毁可视树会崩溃, 必须延后
            RequestThemeApply();
        })));

    private void PackLesson()
    {
        try
        {
            var dir = Services.Config.Current.Capture.Dir;
            var path = Services.Stats.ExportLessonPack(dir, dir);
            ToastService.Instance.Show($"成果包: {path}", "success");
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"打包失败: {ex.Message}", "error");
        }
    }
}
