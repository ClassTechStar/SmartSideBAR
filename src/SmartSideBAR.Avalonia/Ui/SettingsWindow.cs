// 设置窗 (§6.11), 参照 Seelen-UI 设置应用 (导航 + 分页) 与 KoBar (Surface 卡片 +
// 「标签+描述在左 / 控件在右」的 Option 行语法) 重构。保存仍走 ConfigService.Set
// 白名单路径 + 自动广播 ConfigChanged; 控件字段与旧版一一对应, Save() 语义不变。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Design;
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class SettingsWindow : Window
{
    private readonly ConfigService _config;
    private readonly IShellService _shell;
    private readonly IDiagnosticsService _diag;
    private readonly Palette _p;

    private static readonly string[] NavItems = ["外观", "常规", "截图", "录屏", "设备", "快捷键", "诊断", "关于"];

    private readonly ComboBox _theme = new()
    {
        ItemsSource = new[] { "浅色（默认）", "深色", "自动（跟随系统）" },
        SelectedIndex = 0,
        MinWidth = 140,
    };
    private readonly ComboBox _accent = new()
    {
        ItemsSource = new[] { "跟随系统（推荐）", "品牌蓝 #2B6EE0", "青 #12A594", "紫 #7C5CFF", "橙 #D98324", "石墨 #4A5568" },
        SelectedIndex = 0,
        MinWidth = 180,
    };
    private static readonly string[] AccentHex = ["auto", "#2B6EE0", "#12A594", "#7C5CFF", "#D98324", "#4A5568"];

    private readonly CheckBox _glass = new()
    {
        Content = "液态玻璃质感（关闭 = 经典纯白快速面，低配设备推荐）",
    };
    private readonly CheckBox _reduceMotion = new()
    {
        Content = "减弱动效（跳过面板滑入等过渡动画）",
    };
    private readonly ComboBox _side = new() { ItemsSource = new[] { "left", "right" }, SelectedIndex = 1 };
    private readonly ComboBox _trigger = new()
    {
        ItemsSource = new[] { "常驻胶囊（触屏教学推荐）", "极细触发条·点击展开（防误触）", "极细触发条·悬停滑出" },
        SelectedIndex = 0,
        MinWidth = 220,
    };
    private readonly CheckBox _autoLaunch = new() { Content = "开机自启动" };
    private readonly CheckBox _autoCollapse = new() { Content = "全屏应用时自动收起侧栏" };
    private readonly CheckBox _recEnabled = new() { Content = "启用录屏（个别机型媒体管道会崩溃，可关闭）" };
    private readonly ComboBox _format = new() { ItemsSource = new[] { "PNG", "JPG" }, SelectedIndex = 0 };
    private readonly TextBox _captureDir = new() { Watermark = "{Pictures}/Sidekick" };
    private readonly TextBox _recorderDir = new() { Watermark = "{Videos}/Sidekick" };
    private readonly NumericUpDown _fps = new() { Value = 15, Minimum = 5, Maximum = 60 };
    private readonly CheckBox _mic = new() { Content = "录制麦克风 (当前版本仅录画面)" };
    private readonly CheckBox _usbEnabled = new() { Content = "启用 U 盘监控" };
    private readonly NumericUpDown _printerSec = new() { Value = 10, Minimum = 3, Maximum = 300 };
    private readonly TextBox _hkCapture = new();
    private readonly TextBox _hkAnnotate = new();
    private readonly TextBox _hkLongshot = new();
    private readonly TextBox _hkFloatBall = new();
    private readonly TextBox _hkIme = new();
    private readonly TextBlock _diagResult = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };

    private readonly ContentControl _pageHost = new();
    private readonly List<(string Title, Control Page)> _pages = new();
    private Button? _selectedNav;

    public SettingsWindow(ConfigService config, IShellService shell, HotkeyService hotkeys, IDiagnosticsService diag)
    {
        _config = config;
        _shell = shell;
        _diag = diag;
        _p = AppTheme.From(config.Current.Appearance);
        Title = "SmartSideBAR 设置";
        Width = 720;
        Height = 680;
        MinWidth = 640;
        MinHeight = 560;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(_p.Inset);
        LoadFrom(config.Current);
        Content = BuildUi();
        SelectNav(0);
    }

    private void LoadFrom(SidekickConfig c)
    {
        _theme.SelectedIndex = c.Appearance.Theme.ToLowerInvariant() switch
        {
            "dark" => 1,
            "auto" => 2,
            _ => 0,
        };
        _glass.IsChecked = c.Appearance.LiquidGlass;
        _reduceMotion.IsChecked = c.Appearance.ReduceMotion;
        var accentIdx = Array.FindIndex(AccentHex,
            h => string.Equals(h, c.Appearance.Accent, StringComparison.OrdinalIgnoreCase));
        _accent.SelectedIndex = accentIdx < 0 ? 1 : accentIdx; // 未知值回退品牌蓝（index 1）
        _autoCollapse.IsChecked = c.Display.AutoCollapseOnFullscreen;
        _side.SelectedIndex = c.Display.SidebarSide == SidebarSide.Left ? 0 : 1;
        _trigger.SelectedIndex = c.Display.RailTrigger switch
        {
            "edge" => 1,
            "edge-hover" => 2,
            _ => 0,
        };
        _autoLaunch.IsChecked = _shell.GetAutoLaunch();
        _format.SelectedIndex = c.Capture.Format.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        _captureDir.Text = c.Capture.Dir;
        _recorderDir.Text = c.Recorder.Dir;
        _fps.Value = c.Recorder.Fps;
        _mic.IsChecked = c.Recorder.Mic;
        _usbEnabled.IsChecked = c.Usb.Enabled;
        _printerSec.Value = c.Printer.PollIntervalSec;
        _recEnabled.IsChecked = c.Recorder.Enabled;
        _hkCapture.Text = c.Capture.Hotkey;
        _hkAnnotate.Text = c.Capture.AnnotateHotkey;
        _hkLongshot.Text = c.Capture.LongshotHotkey;
        _hkFloatBall.Text = c.FloatBall.Hotkey;
        _hkIme.Text = string.IsNullOrWhiteSpace(c.Ime.Hotkey) ? "Ctrl+Shift+I" : c.Ime.Hotkey;
    }

    // ---------------------------------------------------------------- UI 构建

    private Control BuildUi()
    {
        var nav = new StackPanel { Spacing = 2, Margin = new Thickness(0, 12) };
        for (var i = 0; i < NavItems.Length; i++)
        {
            var index = i;
            var item = new Button
            {
                Content = NavItemContent(NavItems[i]),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12, 0),
                Height = 34,
                CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusS),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
            };
            item.Click += (_, _) => SelectNav(index);
            nav.Children.Add(item);
        }

        _pages.Add(("外观", BuildAppearancePage()));
        _pages.Add(("常规", BuildGeneralPage()));
        _pages.Add(("截图", BuildCapturePage()));
        _pages.Add(("录屏", BuildRecorderPage()));
        _pages.Add(("设备", BuildDevicesPage()));
        _pages.Add(("快捷键", BuildHotkeysPage()));
        _pages.Add(("诊断", BuildDiagnosticsPage()));
        _pages.Add(("关于", BuildAboutPage()));

        var save = new Button
        {
            Content = "保存",
            Padding = new Thickness(24, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
            // 主操作: 品牌色实心 (KoBar「唯一主操作用色」纪律)
            Background = _p.AccentBrush,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusS),
        };
        save.Click += (_, _) => Save();

        var footer = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            BorderBrush = _p.DividerBrush,
            Background = new SolidColorBrush(_p.Card),
            Padding = new Thickness(16, 10),
            Child = save,
        };

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            ColumnDefinitions = new ColumnDefinitions("168,*"),
        };
        var navScroller = new ScrollViewer
        {
            Content = nav,
            Margin = new Thickness(8, 0),
        };
        Grid.SetRow(navScroller, 0);
        Grid.SetColumn(navScroller, 0);
        grid.Children.Add(navScroller);

        Grid.SetRow(_pageHost, 0);
        Grid.SetColumn(_pageHost, 1);
        grid.Children.Add(_pageHost);

        Grid.SetRow(footer, 1);
        Grid.SetColumnSpan(footer, 2);
        grid.Children.Add(footer);
        return grid;
    }

    private static StackPanel NavItemContent(string label)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        sp.Children.Add(new Border
        {
            Width = 3,
            Height = 16,
            CornerRadius = new CornerRadius(DesignTokens.Current.Indicator.Radius),
            // 具体色由 ApplyNavState 落 (paletteAccent), 这里占位透明避免空引用
            Background = Brushes.Transparent,
            IsVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
        });
        sp.Children.Add(new TextBlock { Text = label, FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
        return sp;
    }

    private void SelectNav(int index)
    {
        if (_selectedNav is { } prev)
        {
            ApplyNavState(prev, false);
        }
        if (((Grid)Content!).Children[0] is not ScrollViewer { Content: StackPanel nav }
            || nav.Children[index] is not Button item)
        {
            return;
        }
        _selectedNav = item;
        ApplyNavState(item, true);
        _pageHost.Content = _pages[index].Page;
    }

    private void ApplyNavState(Button btn, bool selected)
    {
        btn.Background = selected ? _p.AccentSoftBrush : Brushes.Transparent;
        if (btn.Content is StackPanel sp)
        {
            if (sp.Children.FirstOrDefault() is Border bar)
            {
                bar.Background = _p.AccentBrush;
                bar.IsVisible = selected;
            }
            if (sp.Children.LastOrDefault() is TextBlock tb)
            {
                tb.FontWeight = selected ? FontWeight.SemiBold : FontWeight.Normal;
                tb.Foreground = selected ? _p.AccentBrush : BodyFg();
            }
        }
    }

    // -------------------------------------------------- 页面（Seelen/KoBar 卡片语法）

    private Control Page(string title, params Control[] sections)
    {
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeight.Bold,
            Foreground = BodyFg(),
            Margin = new Thickness(0, 0, 0, 2),
        });
        foreach (var s in sections) stack.Children.Add(s);
        return new ScrollViewer { Padding = new Thickness(16, 14, 16, 16), Content = stack };
    }

    /// <summary>KoBar Surface 卡片: 卡面 Card 色 + e1 海拔 + 发丝描边, 内含标题与 Option 行。</summary>
    private Control Section(string title, params Control[] rows) => Section(title, null, rows);

    private Control Section(string title, string? hint, params Control[] rows)
    {
        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 12.5,
            FontWeight = FontWeight.SemiBold,
            Foreground = _p.MutedBrush,
            Margin = new Thickness(0, 2, 0, 6),
        });
        if (!string.IsNullOrEmpty(hint))
        {
            stack.Children.Add(new TextBlock
            {
                Text = hint,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                Foreground = _p.MutedBrush,
                Opacity = 0.9,
                Margin = new Thickness(0, 0, 0, 4),
            });
        }
        foreach (var r in rows) stack.Children.Add(r);
        return new Border
        {
            Background = new SolidColorBrush(_p.Card),
            CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusM),
            Padding = new Thickness(14),
            BorderThickness = new Thickness(1),
            BorderBrush = _p.DividerBrush,
            BoxShadow = BoxShadows.Parse(DesignTokens.Current.Shadow.E1),
            Child = stack,
        };
    }

    /// <summary>KoBar SettingsOption 行: 左侧「600 字重标签 + 弱化描述」, 右侧控件 (min 150)。</summary>
    private Control Row(string label, string? description, Control control)
    {
        var left = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        left.Children.Add(new TextBlock { Text = label, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = BodyFg() });
        if (!string.IsNullOrEmpty(description))
        {
            left.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 320,
                Foreground = _p.MutedBrush,
            });
        }

        control.VerticalAlignment = VerticalAlignment.Center;
        switch (control)
        {
            case TextBox box:
                box.MinWidth = 220;
                box.Height = 30;
                break;
            case NumericUpDown num:
                num.MinWidth = 110;
                break;
        }

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            MinHeight = 36,
            Margin = new Thickness(0, 3),
        };
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }

    private Control BuildAppearancePage()
    {
        return Page("外观",
            Section("主题与强调色",
                Row("主题", "浅色 / 深色 / 自动（自动当前按浅色渲染）", _theme),
                Row("强调色", "rail 高亮、选中指示条、主按钮使用", _accent)),
            Section("质感与动效",
                Row("", "液态玻璃为 v1.2 观感; 经典纯白为 1.0 观感, 低配设备推荐", _glass),
                Row("", "跳过面板滑入等过渡动画", _reduceMotion)));
    }

    private Control BuildGeneralPage()
    {
        return Page("常规",
            Section("侧栏",
                Row("侧栏位置", "停靠在屏幕左缘或右缘", _side),
                Row("触发方式", "触发条模式重启后生效; 常驻胶囊即时生效", _trigger),
                Row("", "演示/看片等全屏场景自动收起为底角方块", _autoCollapse),
                Row("", "随系统启动自动运行", _autoLaunch)));
    }

    private Control BuildCapturePage()
    {
        return Page("截图",
            Section("区域截图",
                Row("图片格式", "PNG 无损 / JPG 体积小", _format),
                Row("保存目录", "支持 {Pictures} 等路径占位符", _captureDir)));
    }

    private Control BuildRecorderPage()
    {
        return Page("录屏",
            Section("MP4 录制",
                Row("", "个别机型的媒体管道会在转码准备期崩溃, 可在此关闭", _recEnabled),
                Row("帧率", "5–60 fps, 15 为课堂够用档", _fps),
                Row("", "同时录制麦克风声音", _mic),
                Row("保存目录", "支持 {Videos} 等路径占位符", _recorderDir)));
    }

    private Control BuildDevicesPage()
    {
        return Page("设备",
            Section("外设监控",
                Row("", "插入/移除 U 盘时弹出提醒", _usbEnabled),
                Row("打印机轮询间隔（秒）", "越短发现缺纸/卡纸越及时, 代价是常驻开销", _printerSec)));
    }

    private Control BuildHotkeysPage()
    {
        return Page("快捷键",
            Section("全局热键", "格式: Ctrl/Alt/Shift/Win + 键。冲突时会注册失败, 换一组即可",
                Row("区域截图", "", _hkCapture),
                Row("屏幕批注", "", _hkAnnotate),
                Row("长截图", "", _hkLongshot),
                Row("悬浮球", "", _hkFloatBall),
                Row("输入法切换", "", _hkIme)));
    }

    private Control BuildDiagnosticsPage()
    {
        var runDiag = new Button { Content = "运行诊断" };
        runDiag.Click += (_, _) =>
        {
            _diagResult.Text = "巡检中…";
            Task.Run(() => _diag.RunFullCheck()).ContinueWith(t =>
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _diagResult.Text = t.IsFaulted
                        ? $"诊断失败: {t.Exception?.GetBaseException().Message}"
                        : string.Join('\n', t.Result.Select(kv => $"{kv.Key}: {kv.Value}"));
                });
            }, TaskScheduler.Default);
        };
        var exportDiag = new Button { Content = "导出诊断包", Margin = new Thickness(8, 0, 0, 0) };
        exportDiag.Click += (_, _) =>
        {
            // B13  日志文件被占用时 ZipFile 会抛 IOException; 点击处理器无保护
            // → UI 线程未处理异常, 与 B1 同级别 (直接杀进程)。
            try
            {
                var path = _diag.ExportPack(Path.GetTempPath());
                ToastService.Instance.Show($"诊断包: {path}", "success");
            }
            catch (Exception ex)
            {
                ToastService.Instance.Show($"导出诊断包失败: {ex.Message}", "error");
            }
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(runDiag);
        row.Children.Add(exportDiag);

        // P2-5: 配置备份/恢复(借鉴 ClassIsland 的导入导出入口)。导出会抹掉本机路径类信息。
        var exportCfg = new Button { Content = "导出配置", Margin = new Thickness(8, 0, 0, 0) };
        exportCfg.Click += async (_, _) => await ExportConfigAsync();
        var importCfg = new Button { Content = "导入配置", Margin = new Thickness(8, 0, 0, 0) };
        importCfg.Click += async (_, _) => await ImportConfigAsync();
        var cfgRow = new StackPanel { Orientation = Orientation.Horizontal };
        cfgRow.Children.Add(exportCfg);
        cfgRow.Children.Add(importCfg);

        return Page("诊断",
            Section("自检与排障",
                Row("", "检查系统信息/内存/显示器/各服务健康度", row)),
            Section("配置备份",
                Row("", "导出当前配置（自动脱敏）；导入时只接受已知配置键", cfgRow)),
            new Border
            {
                Background = new SolidColorBrush(_p.Card),
                CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusM),
                Padding = new Thickness(14),
                BorderThickness = new Thickness(1),
                BorderBrush = _p.DividerBrush,
                Child = _diagResult,
            });
    }

    private Control BuildAboutPage()
    {
        var version = typeof(SettingsWindow).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion
            ?? "unknown";
        var stack = new StackPanel { Spacing = 6 };
        stack.Children.Add(new TextBlock
        {
            Text = $"SmartSideBAR v{version}",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = BodyFg(),
        });
        stack.Children.Add(new TextBlock
        {
            Text = "常驻教学大屏侧边的快捷操作与设备提醒中心。\nAvalonia + .NET 单进程实现；配置与 Electron 版互认。",
            FontSize = 12.5,
            TextWrapping = TextWrapping.Wrap,
            Foreground = _p.MutedBrush,
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Apache-2.0 © 2026 ClassTechStar",
            FontSize = 11.5,
            Foreground = _p.MutedBrush,
            Opacity = 0.8,
        });
        return Page("关于",
            new Border
            {
                Background = new SolidColorBrush(_p.Card),
                CornerRadius = new CornerRadius(DesignTokens.Current.Shape.RadiusM),
                Padding = new Thickness(14),
                BorderThickness = new Thickness(1),
                BorderBrush = _p.DividerBrush,
                Child = stack,
            });
    }

    private IBrush BodyFg() => new SolidColorBrush(_p.Dark ? Color.Parse("#E6EBF3") : Color.Parse("#1B2230"));

    // ---------------------------------------------------------------- 保存

    private void Save()
    {
        var c = _config.Current;
        _ = _config.Set("appearance.theme", _theme.SelectedIndex switch
        {
            1 => "dark",
            2 => "auto", // auto 当前按浅色渲染 (AppTheme.IsDark 仅显式 dark 走深色)
            _ => "light",
        });
        _ = _config.Set("appearance.liquidGlass", _glass.IsChecked == true);
        _ = _config.Set("appearance.reduceMotion", _reduceMotion.IsChecked == true);
        _ = _config.Set("appearance.accent",
            AccentHex[Math.Clamp(_accent.SelectedIndex, 0, AccentHex.Length - 1)]);
        _ = _config.Set("display.autoCollapseOnFullscreen", _autoCollapse.IsChecked == true);
        _ = _config.Set("display.sidebarSide", _side.SelectedIndex == 0 ? "left" : "right");
        _ = _config.Set("display.railTrigger", _trigger.SelectedIndex switch
        {
            1 => "edge",
            2 => "edge-hover",
            _ => "always",
        });
        _ = _config.Set("capture.format", (string?)_format.SelectedItem ?? "PNG");
        _ = _config.Set("capture.dir", string.IsNullOrWhiteSpace(_captureDir.Text) ? c.Capture.Dir : _captureDir.Text.Trim());
        _ = _config.Set("recorder.dir", string.IsNullOrWhiteSpace(_recorderDir.Text) ? c.Recorder.Dir : _recorderDir.Text.Trim());
        _ = _config.Set("recorder.fps", (int)(_fps.Value ?? 15));
        _ = _config.Set("recorder.mic", _mic.IsChecked == true);
        _ = _config.Set("recorder.enabled", _recEnabled.IsChecked == true); // B8: 之前漏写, 勾选后重开即丢
        _ = _config.Set("usb.enabled", _usbEnabled.IsChecked == true);
        _ = _config.Set("printer.pollIntervalSec", (int)(_printerSec.Value ?? 10));
        _ = _config.Set("capture.hotkey", _hkCapture.Text?.Trim());
        _ = _config.Set("capture.annotateHotkey", _hkAnnotate.Text?.Trim());
        _ = _config.Set("capture.longshotHotkey", _hkLongshot.Text?.Trim());
        _ = _config.Set("floatBall.hotkey", _hkFloatBall.Text?.Trim());
        _ = _config.Set("ime.hotkey", string.IsNullOrWhiteSpace(_hkIme.Text) ? "Ctrl+Shift+I" : _hkIme.Text?.Trim());
        _shell.SetAutoLaunch(_autoLaunch.IsChecked == true);
        ToastService.Instance.Show("设置已保存", "success");
        Close();
    }

    /// <summary>P2-5: 导出配置(自动脱敏), 写到用户选择的位置。</summary>
    private async Task ExportConfigAsync()
    {
        try
        {
            var node = System.Text.Json.JsonSerializer.SerializeToNode(
                _config.Current, ConfigJsonContext.Default.SidekickConfig);
            if (node is null) return;

            var json = ConfigTransfer.Export(node, desensitize: true);
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "导出配置",
                SuggestedFileName = "smartsidebar-config.json",
                DefaultExtension = "json",
                FileTypeChoices = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
            });
            if (file is null) return;

            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(json);
            ToastService.Instance.Show($"配置已导出: {file.Name}", "success");
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"导出配置失败: {ex.Message}", "error");
        }
    }

    /// <summary>P2-5: 导入配置。逐键走 Set, 由白名单过滤未知键, 策略段不可覆盖。</summary>
    private async Task ImportConfigAsync()
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "导入配置",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("JSON") { Patterns = ["*.json"] }],
            });
            if (files.Count == 0) return;

            await using var stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            var text = await reader.ReadToEndAsync();
            var imported = ConfigTransfer.Import(text);
            if (imported is null)
            {
                ToastService.Instance.Show("配置文件无法解析", "error");
                return;
            }

            var applied = 0;
            foreach (var (path, value) in ConfigTransfer.EnumerateLeaves(imported))
            {
                if (_config.Subscriptions.IsLocked(path)) continue;
                // 版本号不覆盖(由程序自管)
                if (path.Equals("version", StringComparison.OrdinalIgnoreCase)) continue;
                if (_config.Set(path, value)) applied++;
            }
            ToastService.Instance.Show($"已导入 {applied} 项配置（未知键已忽略）", "success");
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"导入配置失败: {ex.Message}", "error");
        }
    }
}
