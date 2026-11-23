// 截图页 VM: 图片格式与保存目录。
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

public sealed class CaptureSettingsViewModel : SettingsPageViewModel
{
    public static readonly string[] FormatOptions = ["PNG", "JPG"];

    private int _formatIndex;
    private string _captureDir = "";
    private string _fallbackDir = "";

    public IReadOnlyList<string> FormatOptionsSource => FormatOptions;

    public override string Title => "截图";

    public int FormatIndex { get => _formatIndex; set => SetProperty(ref _formatIndex, value); }

    public string CaptureDir { get => _captureDir; set => SetProperty(ref _captureDir, value); }

    public override void LoadFrom(SidekickConfig config)
    {
        FormatIndex = config.Capture.Format.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        CaptureDir = config.Capture.Dir;
        _fallbackDir = config.Capture.Dir;
    }

    public override IEnumerable<ConfigChange> CollectChanges() =>
    [
        ConfigChange.From("capture.format", FormatIndex == 1 ? "JPG" : "PNG"),
        ConfigChange.From("capture.dir", string.IsNullOrWhiteSpace(CaptureDir) ? _fallbackDir : CaptureDir.Trim()),
    ];
}
