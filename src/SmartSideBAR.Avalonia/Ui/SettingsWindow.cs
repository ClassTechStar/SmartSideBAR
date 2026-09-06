// 设置窗 (§6.11): 唯一表单语义 (P1-6) —— 保存经 ConfigService.Set 白名单路径, 自动广播 ConfigChanged。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class SettingsWindow : Window
{
    private readonly ConfigService _config;
    private readonly IShellService _shell;
    private readonly IDiagnosticsService _diag;

    private readonly ComboBox _side = new() { ItemsSource = new[] { "left", "right" }, SelectedIndex = 1 };
    private readonly CheckBox _autoLaunch = new() { Content = "开机自启动" };
    private readonly ComboBox _format = new() { ItemsSource = new[] { "PNG", "JPG" }, SelectedIndex = 0 };
    private readonly TextBox _captureDir = new() { Watermark = "{Pictures}/Sidekick" };
    private readonly TextBox _recorderDir = new() { Watermark = "{Videos}/Sidekick" };
    private readonly NumericUpDown _fps = new() { Value = 15, Minimum = 5, Maximum = 60 };
    private readonly CheckBox _mic = new() { Content = "录制系统麦克风 (Wave D)" };
    private readonly CheckBox _usbEnabled = new() { Content = "启用 U 盘监控" };
    private readonly NumericUpDown _printerSec = new() { Value = 10, Minimum = 3, Maximum = 300 };
    private readonly TextBox _hkCapture = new();
    private readonly TextBox _hkAnnotate = new();
    private readonly TextBox _hkLongshot = new();
    private readonly TextBox _hkFloatBall = new();
    private readonly TextBox _hkIme = new();
    private readonly TextBlock _diagResult = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };

    public SettingsWindow(ConfigService config, IShellService shell, HotkeyService hotkeys, IDiagnosticsService diag)
    {
        _config = config;
        _shell = shell;
        _diag = diag;
        Title = "SmartSideBAR 设置";
        Width = 560;
        Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        LoadFrom(config.Current);
        Content = BuildUi();
    }

    private void LoadFrom(SidekickConfig c)
    {
        _side.SelectedIndex = c.Display.SidebarSide == SidebarSide.Left ? 0 : 1;
        _autoLaunch.IsChecked = _shell.GetAutoLaunch();
        _format.SelectedIndex = c.Capture.Format.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _captureDir.Text = c.Capture.Dir;
        _recorderDir.Text = c.Recorder.Dir;
        _fps.Value = c.Recorder.Fps;
        _mic.IsChecked = c.Recorder.Mic;
        _usbEnabled.IsChecked = c.Usb.Enabled;
        _printerSec.Value = c.Printer.PollIntervalSec;
        _hkCapture.Text = c.Capture.Hotkey;
        _hkAnnotate.Text = c.Capture.AnnotateHotkey;
        _hkLongshot.Text = c.Capture.LongshotHotkey;
        _hkFloatBall.Text = c.FloatBall.Hotkey;
        _hkIme.Text = "Ctrl+Shift+I";
    }

    private Control BuildUi()
    {
        static StackPanel RowOf(string label, Control control)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 4) };
            sp.Children.Add(new TextBlock { Text = label, Width = 120, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(control);
            return sp;
        }

        var save = new Button { Content = "保存", Padding = new Thickness(24, 8), HorizontalAlignment = HorizontalAlignment.Right };
        save.Click += (_, _) => Save();

        var runDiag = new Button { Content = "运行诊断" };
        runDiag.Click += (_, _) =>
        {
            _diagResult.Text = "巡检中…";
            Task.Run(() => _diag.RunFullCheck()).ContinueWith(t =>
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _diagResult.Text = string.Join('\n', t.Result.Select(kv => $"{kv.Key}: {kv.Value}"));
                });
            });
        };
        var exportDiag = new Button { Content = "导出诊断包", Margin = new Thickness(8, 0, 0, 0) };
        exportDiag.Click += (_, _) =>
        {
            var path = _diag.ExportPack(Path.GetTempPath());
            ToastService.Instance.Show($"诊断包: {path}", "success");
        };

        var scroll = new ScrollViewer
        {
            Margin = new Thickness(16),
            Content = new StackPanel { Spacing = 6 },
        };
        var root = (StackPanel)scroll.Content!;
        root.Children.Add(new TextBlock { Text = "常规", FontWeight = FontWeight.Bold, FontSize = 15, Margin = new Thickness(0, 8, 0, 4) });
        root.Children.Add(RowOf("侧栏位置", _side));
        root.Children.Add(RowOf("", _autoLaunch));
        root.Children.Add(new TextBlock { Text = "截图", FontWeight = FontWeight.Bold, FontSize = 15, Margin = new Thickness(0, 12, 0, 4) });
        root.Children.Add(RowOf("格式", _format));
        root.Children.Add(RowOf("保存目录", _captureDir));
        root.Children.Add(new TextBlock { Text = "录屏 (MP4)", FontWeight = FontWeight.Bold, FontSize = 15, Margin = new Thickness(0, 12, 0, 4) });
        root.Children.Add(RowOf("帧率", _fps));
        root.Children.Add(RowOf("", _mic));
        root.Children.Add(RowOf("保存目录", _recorderDir));
        root.Children.Add(new TextBlock { Text = "设备", FontWeight = FontWeight.Bold, FontSize = 15, Margin = new Thickness(0, 12, 0, 4) });
        root.Children.Add(RowOf("", _usbEnabled));
        root.Children.Add(RowOf("打印机轮询(秒)", _printerSec));
        root.Children.Add(new TextBlock { Text = "快捷键 (Ctrl/Alt/Shift/Win + 键)", FontWeight = FontWeight.Bold, FontSize = 15, Margin = new Thickness(0, 12, 0, 4) });
        root.Children.Add(RowOf("区域截图", _hkCapture));
        root.Children.Add(RowOf("屏幕批注", _hkAnnotate));
        root.Children.Add(RowOf("长截图", _hkLongshot));
        root.Children.Add(RowOf("悬浮球", _hkFloatBall));
        root.Children.Add(RowOf("输入法", _hkIme));
        root.Children.Add(new TextBlock { Text = "诊断", FontWeight = FontWeight.Bold, FontSize = 15, Margin = new Thickness(0, 12, 0, 4) });
        var diagRow = new StackPanel { Orientation = Orientation.Horizontal };
        diagRow.Children.Add(runDiag);
        diagRow.Children.Add(exportDiag);
        root.Children.Add(diagRow);
        root.Children.Add(_diagResult);
        root.Children.Add(save);

        return scroll;
    }

    private void Save()
    {
        var c = _config.Current;
        _ = _config.Set("display.sidebarSide", _side.SelectedIndex == 0 ? "left" : "right");
        _ = _config.Set("capture.format", (string?)_format.SelectedItem ?? "PNG");
        _ = _config.Set("capture.dir", string.IsNullOrWhiteSpace(_captureDir.Text) ? c.Capture.Dir : _captureDir.Text.Trim());
        _ = _config.Set("recorder.dir", string.IsNullOrWhiteSpace(_recorderDir.Text) ? c.Recorder.Dir : _recorderDir.Text.Trim());
        _ = _config.Set("recorder.fps", (int)(_fps.Value ?? 15));
        _ = _config.Set("usb.enabled", _usbEnabled.IsChecked == true);
        _ = _config.Set("printer.pollIntervalSec", (int)(_printerSec.Value ?? 10));
        _ = _config.Set("capture.hotkey", _hkCapture.Text?.Trim());
        _ = _config.Set("capture.annotateHotkey", _hkAnnotate.Text?.Trim());
        _ = _config.Set("capture.longshotHotkey", _hkLongshot.Text?.Trim());
        _ = _config.Set("floatBall.hotkey", _hkFloatBall.Text?.Trim());
        _shell.SetAutoLaunch(_autoLaunch.IsChecked == true);
        ToastService.Instance.Show("设置已保存", "success");
        Close();
    }
}
