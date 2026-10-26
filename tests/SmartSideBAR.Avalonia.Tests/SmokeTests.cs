// Headless 冒烟 + 像素结构基线 (P1-6)。
//
// 6+1 条链路 (真实控件/服务接线, 不 mock UI):
//   1. Rail 点击 → 面板打开   (headless 鼠标驱动真实窗口)
//   2. 批注: 鼠标画圆 → 转形 → 工具条两级
//   3. 工作区切换 → rail 重建裁剪
//   4. 提醒面板: 种子提醒渲染 + 铃声入口
//   5. Toast → 通知中心留档/清除
//   6. 链接: 服务新增 → 卡片网格 + 分类页签
//   7. 录屏态: 录制中 → 面板计时卡
// (Win32/GDI 依赖的链路: 截图覆盖层/USB WMI/长截图拼接, 由 Core/Windows
//  单测覆盖, headless 无法承载, 此处不伪装。)
//
// 像素结构基线 (3 态): 不做 golden 哈希 (CI 与本机字体/GPU 差异必抖),
// 改断言结构不变量: 胶囊有没有画出来、面板是不是白底、主题对比度对不对。
// 这类缺陷 (如 cbcdfec 玻璃高光根本没画) 是数字基线之外最该拦的。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Avalonia.Threading;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Avalonia.Views;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Links;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.AppBar;

namespace SmartSideBAR.Avalonia.Tests;

public sealed class SmokeTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-smoke-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly EventBus _bus = new();
    private readonly ConfigService _config;

    public SmokeTests()
    {
        Directory.CreateDirectory(_dir);
        var paths = new ConfigPaths(
            UserConfigPath: Path.Combine(_dir, "user.json"),
            ProgramDataConfigPath: Path.Combine(_dir, "pd", "policy.json"),
            LegacyUserConfigPath: null,
            LegacyProgramDataConfigPath: null);
        _config = new ConfigService(paths);
        _config.Load();
        _ = _config.Set("capture.dir", _dir);
        NotificationCenter.Instance.Clear();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    private static Palette Palette => AppTheme.From(null);

    private SidebarServices BuildServices(ConfigService? config = null)
    {
        var c = config ?? _config;
        var appBar = new AppBarService(new FakeAppBarApi(), new FakeHook(), _bus);
        return new SidebarServices(
            c, appBar, new FakeIme(), new FakeShell(), new FakeUsb(), new FakePrinter(),
            new SchedulerService(c, _bus, TimeProvider.System),
            new FakeRecorder(), new FakeSound(), new LinksService(c),
            _ => { });
    }

    private SidebarWindow BuildWindow(ConfigService? config = null)
    {
        var services = BuildServices(config);
        return new SidebarWindow(SmartSideBAR.Windows.AppBar.AppBarEdge.Right, services);
    }

    private static string ExtractText(Control root) =>
        string.Join('\n', root.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));

    private static Control Refreshed(Ui.Panels.SidebarPanel panel)
    {
        panel.RefreshNow(); // Background 优先级的 Post 在 headless RunJobs 下不排空, 用同步重建
        // 挂到窗口: 未进可视树时 Content 的子树不生成, GetVisualDescendants 拿不到文本
        new Window { Width = 420, Height = 720, Content = panel }.Show();
        Pump();
        return panel;
    }

    // ---------------------------------------------------------- 1. rail 点击链路

    [AvaloniaFact]
    public void Rail_click_opens_panel()
    {
        var win = BuildWindow();
        win.Show();
        Pump();

        // 找到 capture 组的首个 rail 按钮, 用 headless 鼠标真实点击 (真实指针/捕获/命令路径)
        var button = win.GetVisualDescendants().OfType<Button>()
            .First(b => b.DataContext is RailItem { Module: "capture" });
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), win)
            ?? throw new InvalidOperationException("无法换算按钮坐标");
        HeadlessWindowExtensions.MouseDown(win, point, MouseButton.Left, RawInputModifiers.None);
        HeadlessWindowExtensions.MouseUp(win, point, MouseButton.Left, RawInputModifiers.None);
        Pump();

        var vm = (SidebarViewModel)win.DataContext!;
        Assert.True(vm.PanelOpen);
        Assert.Equal(PanelKind.Capture, vm.ActivePanel);
        win.Close();
    }

    // ---------------------------------------------------------- 2. 批注转形链路

    [AvaloniaFact]
    public void Annotate_mouse_drawn_circle_becomes_shape()
    {
        // 真实指针链路: 窗口内放 InkCanvas + 透明捕获层 (与 AnnotateWindow 同接线, B5)
        var ink = new InkCanvas { InkToShape = true };
        var host = new Border { Background = global::Avalonia.Media.Brushes.Transparent };
        host.PointerPressed += (_, e) =>
            ink.BeginStroke(e.GetCurrentPoint(host).Position, e.Pointer, host);
        host.PointerMoved += (_, e) =>
        {
            if (e.Pointer.Captured == host) ink.ExtendStroke(e.GetCurrentPoint(host).Position);
        };
        host.PointerReleased += (_, e) =>
        {
            if (e.Pointer.Captured == host) ink.EndStroke();
        };
        var win = new Window { Width = 720, Height = 720 };
        host.Width = 720;
        host.Height = 720;
        var grid = new Grid();
        grid.Children.Add(ink);
        grid.Children.Add(host);
        win.Content = grid;
        win.Show();
        Pump();

        // 鼠标画一个圆 (真实手势点列, 经 PointerPressed/Moved/Released 全链路)
        Point At(double t) => new(360 + 120 * Math.Cos(t), 360 + 120 * Math.Sin(t));
        var p0 = At(0);
        HeadlessWindowExtensions.MouseDown(win, p0, MouseButton.Left, RawInputModifiers.None);
        for (var i = 1; i <= 40; i++)
        {
            HeadlessWindowExtensions.MouseMove(win, At(i * 2 * Math.PI / 40), RawInputModifiers.None);
        }
        HeadlessWindowExtensions.MouseUp(win, At(2 * Math.PI), MouseButton.Left, RawInputModifiers.None);
        Pump();

        // 收笔即转形: 引擎内只剩一条干净 Shape 笔画 (798→拆分后引擎行为不变)
        Assert.Equal(1, ink.Count);

        // 工具条两级: 默认级可构建, 未展开
        var toolbar = new AnnotateToolbar(ink, save: () => { }, close: () => { },
            saveInkOnly: () => { }, defaultColorIndex: 1);
        Assert.False(toolbar.Expanded);
        Assert.NotNull(toolbar.Host.Child);
        win.Close();
    }

    // ---------------------------------------------------------- 3. 工作区链路

    [AvaloniaFact]
    public void Workspace_switch_rebuilds_rail()
    {
        var vm = new SidebarViewModel(BuildServices(), _config.Current.Links);
        Assert.Contains(vm.RailItems, i => i.Module == "usb"); // 默认工作区全量

        _ = _config.Set("workspace.current", "批注");
        vm.RebuildRail();

        Assert.DoesNotContain(vm.RailItems, i => i.Module == "usb");
        Assert.DoesNotContain(vm.RailItems, i => i.Module == "record");
        Assert.Contains(vm.RailItems, i => i.Module == "annotate");
        Assert.Contains(vm.RailItems, i => i.Module == "ime");

        _ = _config.Set("workspace.current", "默认");
        vm.RebuildRail();
        Assert.Contains(vm.RailItems, i => i.Module == "usb"); // 切回恢复
    }

    // ---------------------------------------------------------- 4. 提醒面板链路

    [AvaloniaFact]
    public void Reminder_panel_renders_seeded_reminder()
    {
        var services = BuildServices();
        var due = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeMilliseconds();
        services.Scheduler.AddReminder(ReminderKind.Once, due, "下课前关投影"); // 与面板同一实例

        var panel = Refreshed(new Ui.Panels.ReminderPanel(services, Palette));
        var text = ExtractText(panel);
        Assert.Contains("下课前关投影", text);
        Assert.Contains("待触发", text);
        Assert.Contains("铃声设置", text); // 非空列表仍保留铃声入口
    }

    // ---------------------------------------------------------- 5. 通知中心链路

    [AvaloniaFact]
    public void Toast_to_notification_center_chain()
    {
        ToastService.Instance.Show("U 盘已接入: E: 课件盘 32GB", "info");
        ToastService.Instance.Show("录屏完成: record.mp4", "success");
        Pump();

        Assert.Equal(2, NotificationCenter.Instance.Items.Count);
        Assert.Equal("录屏完成: record.mp4", NotificationCenter.Instance.Items[0].Message); // 最新在前

        var id = NotificationCenter.Instance.Items[1].Id;
        NotificationCenter.Instance.Remove(id);
        Assert.Single(NotificationCenter.Instance.Items);

        NotificationCenter.Instance.Clear();
        Assert.Empty(NotificationCenter.Instance.Items);
    }

    // ---------------------------------------------------------- 6. 链接链路

    [AvaloniaFact]
    public void Links_add_then_panel_grid_and_tab()
    {
        var links = new LinksService(_config);
        links.Add("DeepSeek", "https://chat.deepseek.com", category: "AI");
        links.Add("国家中小学智慧教育平台", "https://basic.smartedu.cn", category: "网站");

        var opened = new List<string>();
        var panel = Refreshed(new Ui.Panels.LinksPanel(BuildServices(), Palette, l => opened.Add(l.Url)));
        var text = ExtractText(panel);
        Assert.Contains("DeepSeek", text);
        Assert.Contains("全部", text);
        Assert.Contains("AI", text);

        var ds = links.List().First(l => l.Name == "DeepSeek");
        Assert.Equal("https://chat.deepseek.com", ds.Url);
    }

    // ---------------------------------------------------------- 7. 录屏态链路

    [AvaloniaFact]
    public void Capture_panel_shows_recording_card_when_recording()
    {
        var recorder = new FakeRecorder { IsRecording = true };
        var services = BuildServices() with { Recorder = recorder };
        var panel = Refreshed(new Ui.Panels.CapturePanel(services, Palette));
        var text = ExtractText(panel);
        Assert.Contains("停止", text); // 录制中 → 计时卡带停止按钮, 而非动作磁贴
    }

    // ========================================================== 像素结构基线

    [AvaloniaFact]
    public void Pixel_baseline_rail_capsule_is_drawn()
    {
        var win = BuildWindow();
        win.Show();
        Pump();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        using var frame = win.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(72, frame!.PixelSize.Width); // rail 基准宽 (design-tokens)

                // 胶囊中央列必须大面积不透明且亮色 (经典白面), 防"没画出来"这类缺陷
        var samples = ReadColumn(frame, frame.PixelSize.Width / 2);
        var opaque = samples.Count(p => p.a > 0x40);
        Assert.True(opaque > frame.PixelSize.Height * 0.5,
            $"胶囊中央列不透明像素仅 {opaque}/{frame.PixelSize.Height}");
        Assert.True(samples.Where(p => p.a > 0x40).Average(p => (p.r + p.g + p.b) / 3.0) > 200,
            "胶囊底应为亮色 (经典白面出厂默认)");
        win.Close();
    }

    [AvaloniaFact]
    public void Pixel_baseline_panel_opaque_white_region()
    {
        // reduceMotion: 跳过滑入动画, 面板直接全宽 (headless 无动画时钟, 顺带覆盖该分支)
        _ = _config.Set("appearance.reduceMotion", true);
        var win = BuildWindow();
        win.Width = 452; // rail 72 + panel 380 (AppBar 授予宽)
        win.Show();
        Pump();
        var vm = (SidebarViewModel)win.DataContext!;
        vm.OpenPanel(PanelKind.Settings);
        Pump();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();

        using var frame = win.CaptureRenderedFrame();
        Assert.NotNull(frame);
        Assert.Equal(452, frame!.PixelSize.Width);

                // 面板区域 (rail 右侧 380px) 应大面积不透明白, 确认面板底画出来了
        foreach (var x in new[] { 100, 200, 300, 420 })
        {
            var col = ReadColumn(frame, x);
            var opaqueWhite = col.Count(p => p.a > 0x40 && p.r > 230 && p.g > 230 && p.b > 230);
            Assert.True(opaqueWhite > frame.PixelSize.Height * 0.4,
                $"x={x} 列不透明白像素仅 {opaqueWhite}/{frame.PixelSize.Height}");
        }
        win.Close();
    }

    [AvaloniaFact]
    public void Pixel_baseline_theme_contrast_flips()
    {
        var light = BuildWindow();
        light.Show();
        Pump();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using (var frame = light.CaptureRenderedFrame())
        {
            Assert.NotNull(frame);
            var lum = AverageLuminance(frame!, 36);
            Assert.True(lum > 180, $"浅色胶囊亮度 {lum:F0} 应显著偏亮");
        }
        light.Close();

        _ = _config.Set("appearance.theme", "dark");
        var dark = BuildWindow();
        dark.Show();
        Pump();
        global::Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using (var frame = dark.CaptureRenderedFrame())
        {
            Assert.NotNull(frame);
            var lum = AverageLuminance(frame!, 36);
            Assert.True(lum < 120, $"深色胶囊亮度 {lum:F0} 应显著偏暗 (主题未生效?)");
        }
        dark.Close();
    }

    // ---- 像素读取工具 ----

    private static (byte a, byte r, byte g, byte b)[] ReadColumn(Bitmap frame, int x)
    {
        var h = frame.PixelSize.Height;
        var buffer = new byte[4 * h];
        unsafe
        {
            fixed (byte* p = buffer)
            {
                frame.CopyPixels(new PixelRect(x, 0, 1, h), (nint)p, buffer.Length, 4);
            }
        }
        var result = new (byte, byte, byte, byte)[h];
        for (var i = 0; i < h; i++)
        {
            // R/B 通道序对断言 (白/亮度) 对称, 无需区分
            result[i] = (buffer[i * 4 + 3], buffer[i * 4 + 2], buffer[i * 4 + 1], buffer[i * 4]);
        }
        return result;
    }

    private static double AverageLuminance(Bitmap frame, int x)
    {
        var col = ReadColumn(frame, x).Where(p => p.a > 0x40);
        return col.Any() ? col.Average(p => 0.299 * p.r + 0.587 * p.g + 0.114 * p.b) : 0;
    }
}
