// 录屏页 VM: 总开关/帧率/麦克风(未支持)/保存目录。
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

public sealed class RecorderSettingsViewModel : SettingsPageViewModel
{
    private bool _enabled = true;
    private double _fps = 15;
    private string _recorderDir = "";
    private string _fallbackDir = "";
    private bool _mic;

    public override string Title => "录屏";

    public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }

    public double Fps { get => _fps; set => SetProperty(ref _fps, value); }

    public string RecorderDir { get => _recorderDir; set => SetProperty(ref _recorderDir, value); }

    /// <summary>麦克风混流尚未实现: 只读展示, 不假装可配。</summary>
    public bool Mic { get => _mic; private set => SetProperty(ref _mic, value); }

    public override void LoadFrom(SidekickConfig config)
    {
        Enabled = config.Recorder.Enabled;
        Fps = config.Recorder.Fps;
        RecorderDir = config.Recorder.Dir;
        _fallbackDir = config.Recorder.Dir;
        Mic = false;
    }

    public override IEnumerable<ConfigChange> CollectChanges() =>
    [
        ConfigChange.From("recorder.enabled", Enabled),
        ConfigChange.From("recorder.fps", (int)Math.Clamp(Fps, 5, 60)),
        ConfigChange.From("recorder.mic", false),
        ConfigChange.From("recorder.dir", string.IsNullOrWhiteSpace(RecorderDir) ? _fallbackDir : RecorderDir.Trim()),
    ];
}
