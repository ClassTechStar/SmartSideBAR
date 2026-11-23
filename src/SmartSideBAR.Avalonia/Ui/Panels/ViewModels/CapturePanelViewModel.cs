// M3: 截图与批注面板 VM（动作磁贴 / 录制计时 / 最近文件）。
using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia.Media;
using Avalonia.Threading;
using SmartSideBAR.Avalonia.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Panels.ViewModels;

internal sealed record CaptureAction(string Label, IImage? Icon, ICommand Command);

internal sealed record RecentFileRow(string Name, string TimeText, ICommand RevealCommand);

internal sealed class CapturePanelViewModel : PanelViewModel
{
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _recording;
    private bool _paused;
    private string _elapsedText = "00:00";
    private string _formatLine = "";
    private bool _recentEmpty = true;

    public CapturePanelViewModel(SidebarServices services, Palette palette) : base(services, palette)
    {
        _ticker.Tick += (_, _) => Tick();

        StopCommand = new RelayCommand(_ =>
        {
            Services.Dispatch("record");
            DispatcherTimer.RunOnce(RequestRefresh, TimeSpan.FromMilliseconds(900));
        });
        PauseCommand = new RelayCommand(_ =>
        {
            if (Services.Recorder.IsPaused) Services.Recorder.Resume();
            else Services.Recorder.Pause();
            Reload();
        });
    }

    /// <summary>动作磁贴（图标按当前主题明暗染色, 主题切换后随 Reload 重建）。</summary>
    public IReadOnlyList<CaptureAction> Actions { get; private set; } = [];

    private IReadOnlyList<CaptureAction> BuildActions() =>
    [
        new CaptureAction("区域截图", SvgIcons.Load("capture", Palette.Dark),
            new RelayCommand(_ => Services.Dispatch("capture"))),
        new CaptureAction("批注", SvgIcons.Load("annotate", Palette.Dark),
            new RelayCommand(_ => Services.Dispatch("annotate"))),
        new CaptureAction("长截图", SvgIcons.Load("longshot", Palette.Dark),
            new RelayCommand(_ => Services.Dispatch("longshot"))),
        new CaptureAction("录屏", SvgIcons.Load("record", Palette.Dark),
            new RelayCommand(_ =>
            {
                Services.Dispatch("record");
                // 录制启动有异步开销，稍后再刷新成计时态
                DispatcherTimer.RunOnce(RequestRefresh, TimeSpan.FromMilliseconds(600));
            })),
        new CaptureAction("板中板", SvgIcons.Load("sidebar", Palette.Dark),
            new RelayCommand(_ => Services.Dispatch("miniboard"))),
    ];

    public ObservableCollection<RecentFileRow> RecentFiles { get; } = [];

    public bool IsRecording
    {
        get => _recording;
        private set => SetProperty(ref _recording, value);
    }

    public bool IsPaused
    {
        get => _paused;
        private set => SetProperty(ref _paused, value);
    }

    public string PauseText => IsPaused ? "继续" : "暂停";

    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
    }

    public string FormatLine
    {
        get => _formatLine;
        private set => SetProperty(ref _formatLine, value);
    }

    public bool IsRecentEmpty
    {
        get => _recentEmpty;
        private set => SetProperty(ref _recentEmpty, value);
    }

    public RelayCommand StopCommand { get; }

    public RelayCommand PauseCommand { get; }

    public override void Reload()
    {
        var cfg = Services.Config.Current.Capture;
        Actions = BuildActions();
        Raise(nameof(Actions));
        IsRecording = Services.Recorder.IsRecording;
        IsPaused = Services.Recorder.IsPaused;
        Raise(nameof(PauseText));
        FormatLine = $"保存格式 {cfg.Format.ToUpperInvariant()} · 目录 {Shorten(cfg.Dir)}";

        // 录屏可能由热键/悬浮球启动，面板打开时按真实状态决定是否计时
        if (IsRecording)
        {
            ElapsedText = ElapsedTextOf();
            if (!_ticker.IsEnabled) _ticker.Start();
        }
        else
        {
            _ticker.Stop();
        }

        LoadRecent();
    }

    private void Tick()
    {
        if (!Services.Recorder.IsRecording)
        {
            _ticker.Stop();
            RequestRefresh();
            return;
        }
        ElapsedText = ElapsedTextOf();
    }

    // P1-6: 计时用净时长 (扣除暂停段), 与成片时间线一致
    private string ElapsedTextOf()
    {
        var span = Services.Recorder.Elapsed;
        return $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
    }

    private void LoadRecent()
    {
        RecentFiles.Clear();
        foreach (var f in FindRecent())
        {
            var path = f.FullName;
            RecentFiles.Add(new RecentFileRow(
                Path.GetFileName(path),
                f.LastWriteTime.ToString("MM-dd HH:mm"),
                new RelayCommand(_ => Services.Shell.RevealInExplorer(path))));
        }
        IsRecentEmpty = RecentFiles.Count == 0;
    }

    private List<FileInfo> FindRecent()
    {
        try
        {
            var dir = Environment.ExpandEnvironmentVariables(Services.Config.Current.Capture.Dir);
            if (!Directory.Exists(dir)) return [];
            return [.. Directory.GetFiles(dir)
                .Where(f => f.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .Take(5)];
        }
        catch
        {
            return [];
        }
    }

    private static string Shorten(string dir)
    {
        var expanded = Environment.ExpandEnvironmentVariables(dir);
        return expanded.Length > 34 ? "…" + expanded[^33..] : expanded;
    }
}
