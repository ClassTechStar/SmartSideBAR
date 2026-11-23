// M3: 提醒与通知面板 VM（提醒页签 / 通知中心页签 / 铃声设置折叠区）。
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Scheduling;

namespace SmartSideBAR.Avalonia.Ui.Panels.ViewModels;

internal sealed record ReminderTab(string Name, bool IsActive, ICommand Command);

internal sealed record ReminderRow(
    string Note, string WhenText, ICommand SnoozeCommand, ICommand DeleteCommand);

internal sealed record NotificationRow(string Message, string TimeText, IBrush DotBrush, ICommand DismissCommand);

internal sealed record SoundPreset(string Label, bool IsActive, ICommand Command);

internal sealed class ReminderPanelViewModel : PanelViewModel
{
    private static readonly (string Value, string Label)[] PresetOptions =
    [
        ("default", "默认"), ("bell", "铃声"), ("chime", "提示"), ("alarm", "警报"),
    ];

    private int _tab; // 0=提醒 1=通知（Seelen 通知中心页签）
    private int _lastTab = -1;
    private bool _soundExpanded;
    private bool _loading;
    private double _volume;
    private double _repeat;
    private string? _mp3Path;

    public ReminderPanelViewModel(SidebarServices services, Palette palette) : base(services, palette)
    {
        NewReminderCommand = new RelayCommand(_ => Services.Dispatch("reminder"));
        ClearNotificationsCommand = new RelayCommand(_ => NotificationCenter.Instance.Clear());
        ToggleSoundCommand = new RelayCommand(_ => SoundExpanded = !SoundExpanded);
        PreviewCommand = new RelayCommand(_ => Preview());
        ClearMp3Command = new RelayCommand(_ =>
        {
            Services.Config.Set("reminderSound.mp3Path", null);
            RequestRefresh();
        });
    }

    public ObservableCollection<ReminderTab> Tabs { get; } = [];

    public ObservableCollection<ReminderRow> Reminders { get; } = [];

    public ObservableCollection<NotificationRow> Notifications { get; } = [];

    public ObservableCollection<SoundPreset> Presets { get; } = [];

    public bool ShowReminders => _tab == 0;

    public bool ShowNotifications => _tab == 1;

    public bool RemindersEmpty { get; private set; } = true;

    public string ReminderCountText => $"待触发（{Reminders.Count}）";

    public bool NotificationsEmpty { get; private set; } = true;

    public string NotificationCountText => $"通知动态（{Notifications.Count}）";

    /// <summary>P2-3: 页签切换方向（0 = 首次构建, 不动画）。</summary>
    public int EnterDirection { get; private set; }

    public bool SoundExpanded
    {
        get => _soundExpanded;
        private set
        {
            if (!SetProperty(ref _soundExpanded, value)) return;
            Raise(nameof(SoundChevron));
        }
    }

    public string SoundChevron => SoundExpanded ? "▾" : "▸";

    public double Volume
    {
        get => _volume;
        set
        {
            if (!SetProperty(ref _volume, value)) return;
            Raise(nameof(VolumeText));
            if (_loading) return;
            Services.Config.Set("reminderSound.volume", Math.Round(value) / 100.0);
        }
    }

    public string VolumeText => $"音量 {Math.Round(Volume)}%";

    public double Repeat
    {
        get => _repeat;
        set
        {
            if (!SetProperty(ref _repeat, value)) return;
            Raise(nameof(RepeatText));
            if (_loading) return;
            Services.Config.Set("reminderSound.repeat", (int)Math.Round(value));
        }
    }

    public string RepeatText => $"重复次数 {(int)Math.Round(Repeat)} 次";

    public bool HasMp3 => !string.IsNullOrWhiteSpace(_mp3Path);

    public string PickMp3Text => HasMp3 ? "更换 MP3" : "选择 MP3 文件";

    public string Mp3FileName => string.IsNullOrWhiteSpace(_mp3Path) ? "" : Path.GetFileName(_mp3Path);

    public IImage? EmptyBellIcon => SvgIcons.Load("bell", Palette.Dark);

    public RelayCommand NewReminderCommand { get; }

    public RelayCommand ClearNotificationsCommand { get; }

    public RelayCommand ToggleSoundCommand { get; }

    public RelayCommand PreviewCommand { get; }

    public RelayCommand ClearMp3Command { get; }

    public override void Reload()
    {
        var cfg = Services.Config.Current.ReminderSound;

        _loading = true;
        try
        {
            Volume = Math.Round(cfg.Volume * 100);
            Repeat = cfg.Repeat;
            _mp3Path = cfg.Mp3Path;
            Raise(nameof(HasMp3));
            Raise(nameof(PickMp3Text));
            Raise(nameof(Mp3FileName));
        }
        finally
        {
            _loading = false;
        }

        EnterDirection = _lastTab < 0 ? 0 : (_tab >= _lastTab ? 1 : -1);
        _lastTab = _tab;

        BuildTabs();
        if (_tab == 1) BuildNotifications();
        else BuildReminders();
        BuildPresets(cfg);

        Raise(nameof(ShowReminders));
        Raise(nameof(ShowNotifications));
        Raise(nameof(EmptyBellIcon));
    }

    /// <summary>页签（Seelen segmented 语法: 选中 = accent 实心）。</summary>
    private void BuildTabs()
    {
        string[] names = ["提醒", "通知"];
        Tabs.Clear();
        for (var i = 0; i < names.Length; i++)
        {
            var index = i;
            Tabs.Add(new ReminderTab(
                names[i],
                _tab == index,
                new RelayCommand(_ => SelectTab(index))));
        }
    }

    private void SelectTab(int index)
    {
        if (_tab == index) return;
        _tab = index;
        RequestRefresh();
    }

    /// <summary>通知中心页: 计数 + 全部清除 + 卡片列表。</summary>
    private void BuildNotifications()
    {
        var center = NotificationCenter.Instance;
        Notifications.Clear();
        foreach (var n in center.Items)
        {
            var id = n.Id;
            Notifications.Add(new NotificationRow(
                n.Message,
                n.Time.ToString("MM-dd HH:mm"),
                Brush(ToneColor(n.Tone)),
                new RelayCommand(_ => center.Remove(id))));
        }

        NotificationsEmpty = Notifications.Count == 0;
        Raise(nameof(NotificationsEmpty));
        Raise(nameof(NotificationCountText));
    }

    private void BuildReminders()
    {
        IReadOnlyList<Reminder> items;
        try { items = Services.Scheduler.ListReminders(); }
        catch { items = []; }

        Reminders.Clear();
        foreach (var r in items.OrderBy(x => x.At))
        {
            var when = DateTimeOffset.FromUnixTimeMilliseconds(r.At).ToLocalTime();
            var id = r.Id;
            Reminders.Add(new ReminderRow(
                string.IsNullOrWhiteSpace(r.Note) ? "（无备注）" : r.Note,
                $"{when:MM-dd HH:mm} · {KindText(r.Kind, r.RepeatMin)}",
                new RelayCommand(_ =>
                {
                    Services.Scheduler.Snooze(id, 10);
                    RequestRefresh();
                }),
                new RelayCommand(_ =>
                {
                    Services.Scheduler.RemoveReminder(id);
                    RequestRefresh();
                })));
        }

        RemindersEmpty = Reminders.Count == 0;
        Raise(nameof(RemindersEmpty));
        Raise(nameof(ReminderCountText));
    }

    /// <summary>铃声类型（对应配置 reminderSound.preset）。</summary>
    private void BuildPresets(ReminderSoundConfig cfg)
    {
        Presets.Clear();
        foreach (var (value, label) in PresetOptions)
        {
            var preset = value;
            Presets.Add(new SoundPreset(
                label,
                string.Equals(cfg.Preset, value, StringComparison.OrdinalIgnoreCase),
                new RelayCommand(_ =>
                {
                    Services.Config.Set("reminderSound.preset", preset);
                    RequestRefresh();
                })));
        }
    }

    /// <summary>文件选择框在 code-behind（需要 TopLevel.StorageProvider），选完回填配置。</summary>
    public void SetMp3Path(string path)
    {
        Services.Config.Set("reminderSound.mp3Path", path);
        ToastService.Instance.Show("铃声已更新，点“试听铃声”确认", "success");
        RequestRefresh();
    }

    private void Preview()
    {
        try
        {
            _ = Services.Sound.PlayReminderAsync(Services.Config.Current.ReminderSound);
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"试听失败: {ex.Message}", "error");
        }
    }

    private static Color ToneColor(string tone) => tone switch
    {
        "success" => AppTheme.Ok,
        "warn" => AppTheme.Warn,
        "error" => AppTheme.Err,
        _ => AppTheme.Ok,
    };

    private static string KindText(ReminderKind kind, int? repeatMin) => kind switch
    {
        ReminderKind.Once => "一次性",
        ReminderKind.Interval => $"每 {repeatMin ?? 0} 分钟",
        ReminderKind.Hourly => "整点提醒",
        _ => "提醒",
    };
}
