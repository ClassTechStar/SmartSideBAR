// OOBE 向导 (§6.11): 6 步流程 + 角色裁剪 + 环境真实探测 (P1-12/P2-3 语义); OobeState 字段级合并 (E2 根修)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Input;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class OobeWindow : Window
{
    private readonly ConfigService _config;
    private readonly IImeService _ime;
    private readonly IPrinterMonitor _printer;
    private int _step;

    private readonly StackPanel _body = new() { Spacing = 12 };
    private readonly TextBlock _title = new() { FontSize = 22, FontWeight = FontWeight.Bold };
    private readonly TextBlock _envText = new() { TextWrapping = TextWrapping.Wrap, FontSize = 13 };
    private readonly CheckBox _prefIme = new() { Content = "输入法切换", IsChecked = true };
    private readonly CheckBox _prefUsb = new() { Content = "U 盘监控", IsChecked = true };
    private readonly CheckBox _prefShot = new() { Content = "截图/批注", IsChecked = true };
    private readonly CheckBox _prefRecorder = new() { Content = "录屏", IsChecked = false };
    private readonly CheckBox _prefPrinter = new() { Content = "打印机状态", IsChecked = true };
    private string _role = "teacher";

    public OobeWindow(ConfigService config, IImeService ime, IPrinterMonitor printer)
    {
        _config = config;
        _ime = ime;
        _printer = printer;
        Width = 560;
        Height = 480;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Title = "SmartSideBAR 初始化向导";
        RenderStep();
    }

    private void RenderStep()
    {
        _body.Children.Clear();
        var next = new Button { Content = _step >= 5 ? "完成" : "下一步", Padding = new Thickness(20, 8), HorizontalAlignment = HorizontalAlignment.Right };
        next.Click += (_, _) => Advance();
        var back = new Button { Content = "上一步", Padding = new Thickness(20, 8) };
        back.Click += (_, _) => { if (_step > 0) { _step--; RenderStep(); } };
        var skip = new Button { Content = "跳过向导" };
        skip.Click += (_, _) => Complete(skipped: true);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        if (_step > 0) buttons.Children.Add(back);
        buttons.Children.Add(next);

        _body.Children.Add(new TextBlock { Text = $"第 {_step + 1} / 6 步", Foreground = Brushes.Gray, FontSize = 12 });
        switch (_step)
        {
            case 0:
                _title.Text = "欢迎使用 SmartSideBAR";
                _body.Children.Add(_title);
                _body.Children.Add(new TextBlock
                {
                    Text = "教学大屏常驻助手: 侧栏快捷操作 · 输入法切换 · U 盘提醒 · 截图批注 · 录屏。\n\n侧栏将停靠屏幕右缘, 任何窗口最大化都不会遮挡它。",
                    TextWrapping = TextWrapping.Wrap,
                });
                break;
            case 1:
                _title.Text = "你的角色";
                _body.Children.Add(_title);
                var teacher = new RadioButton { Content = "教师 —— 常用教学功能 (推荐)", GroupName = "role", IsChecked = _role == "teacher" };
                var admin = new RadioButton { Content = "管理员 —— 全部功能 + 策略下发", GroupName = "role", IsChecked = _role == "admin" };
                teacher.IsCheckedChanged += (_, _) => _role = "teacher";
                admin.IsCheckedChanged += (_, _) => _role = "admin";
                _body.Children.Add(teacher);
                _body.Children.Add(admin);
                break;
            case 2:
                _title.Text = "选择要启用的功能";
                _body.Children.Add(_title);
                _body.Children.Add(_prefIme);
                _body.Children.Add(_prefUsb);
                _body.Children.Add(_prefShot);
                _body.Children.Add(_prefRecorder);
                _body.Children.Add(_prefPrinter);
                break;
            case 3:
                _title.Text = "环境检测";
                _body.Children.Add(_title);
                _envText.Text = "检测中…";
                _body.Children.Add(_envText);
                Task.Run(() =>
                {
                    var printers = _printer.Query();
                    var imeState = _ime.GetState();
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        var screen = Screens.Primary?.Bounds ?? new PixelRect(0, 0, 1920, 1080);
                        _envText.Text = $"屏幕: {screen.Width}x{screen.Height} (DIP)\n" +
                                        $"输入法: {imeState.Locale} (中文={imeState.IsChinese})\n" +
                                        $"打印机: {printers.Count} 台\n" +
                                        $"录屏: {(global::Windows.Graphics.Capture.GraphicsCaptureSession.IsSupported() ? "可用" : "不可用")}";
                    });
                });
                break;
            case 4:
                _title.Text = "快捷键";
                _body.Children.Add(_title);
                _body.Children.Add(new TextBlock
                {
                    Text = "Ctrl+Shift+A  区域截图\nCtrl+Shift+D  屏幕批注\nCtrl+Shift+L  长截图\nAlt+Q        悬浮球\n\n可在设置的快捷键页修改; 与其他软件冲突时系统会明确告知并给出替代建议。",
                    TextWrapping = TextWrapping.Wrap,
                });
                break;
            case 5:
                _title.Text = "一切就绪";
                _body.Children.Add(_title);
                _body.Children.Add(new TextBlock
                {
                    Text = "侧栏已停靠屏幕右缘; 悬浮球可在屏幕边缘拖拽。托盘图标提供设置与退出入口。",
                    TextWrapping = TextWrapping.Wrap,
                });
                break;
        }
        _body.Children.Add(buttons);

        var outer = new ScrollViewer { Content = new StackPanel { Margin = new Thickness(24), Children = { _body, skip } } };
        Content = outer;
    }

    private void Advance()
    {
        if (_step >= 5) { Complete(skipped: false); return; }
        _step++;
        RenderStep();
    }

    /// <summary>E2 根修: OobeState 字段级合并 —— 仅写本次向导涉及的字段, 保留其他 (record with)。</summary>
    private void Complete(bool skipped)
    {
        var current = _config.Current.Oobe;
        var prefs = new OobePrefs
        {
            Ime = _prefIme.IsChecked == true,
            Usb = _prefUsb.IsChecked == true,
            Shot = _prefShot.IsChecked == true,
            Recorder = _prefRecorder.IsChecked == true,
            Printer = _prefPrinter.IsChecked == true,
        };
        var state = current with
        {
            Completed = !skipped,
            CompletedAt = DateTime.Now.ToString("o"),
            Skipped = skipped,
            Role = _role,
            LastStepIndex = _step,
            Prefs = prefs,
        };
        _ = _config.Set("oobe", state);

        // 角色裁剪 → disabledModules (P2-3 语义)
        if (!skipped && _role == "teacher")
        {
            var disabled = new List<string>();
            if (prefs.Recorder) { /* keep recorder on */ } else disabled.Add("recorder");
            _ = _config.Set("policy.disabledModules", disabled);
        }
        Close();
    }
}
