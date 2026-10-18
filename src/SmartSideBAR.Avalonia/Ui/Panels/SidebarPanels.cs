// 侧栏各功能面板 —— 对应 v1.2 components/*Panel.vue。
// v1.2 有 8 个面板（输入法/截图/USB/打印机/链接/提醒/任务管理/设置），Avalonia 版此前只剩
// "快捷链接"一个，其余 rail 图标点了只弹 Toast —— 这是最明显的一处退化。此处按 v1.2 的内容与
// 信息层级重建，全部以代码构建 UI（与本项目 SettingsWindow / ReminderWindow 的做法一致）。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Avalonia.Views;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Devices;
using SmartSideBAR.Windows.Devices;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal abstract class SidebarPanel : UserControl
{
    private bool _pending;

    protected SidebarPanel(SidebarServices services, Palette palette)
    {
        Services = services;
        Palette = palette;
    }

    protected SidebarServices Services { get; }

    protected Palette Palette { get; }

    /// <summary>构建面板内容。每次刷新都会重新构建（面板内容轻量，可整体替换）。
    /// 注意：不要在子控件的事件处理器里同步重建面板 —— 控件会在派发事件过程中被销毁，
    /// Avalonia 的原生渲染层会因此崩溃（无托管异常、无崩溃日志）。统一走 Refresh()。</summary>
    protected abstract Control Build();

    /// <summary>内容构建完成后的钩子（例如启动/停止计时器）。</summary>
    protected virtual void OnBuilt() { }

    /// <summary>请求刷新：合并重复请求并延后到 Background 优先级执行。</summary>
    public void Refresh()
    {
        if (_pending) return;
        _pending = true;
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _pending = false;
            Content = Build();
            OnBuilt();
        }, global::Avalonia.Threading.DispatcherPriority.Background);
    }

    protected StackPanel Stack(double spacing = 12) => new() { Spacing = spacing };
}

// ------------------------------------------------------------------ 截图与批注
internal sealed class CapturePanel : SidebarPanel
{
    private readonly global::Avalonia.Threading.DispatcherTimer _ticker =
        new() { Interval = TimeSpan.FromSeconds(1) };
    private TextBlock? _elapsed;

    public CapturePanel(SidebarServices s, Palette p) : base(s, p)
    {
        _ticker.Tick += (_, _) => TickRecording();
    }

    protected override void OnBuilt()
    {
        // 录屏可能由热键/悬浮球启动，面板打开时按真实状态决定是否计时
        if (Services.Recorder.IsRecording) _ticker.Start();
        else _ticker.Stop();
    }

    private void TickRecording()
    {
        if (!Services.Recorder.IsRecording)
        {
            _ticker.Stop();
            Refresh();
            return;
        }
        if (_elapsed is not null) _elapsed.Text = ElapsedText();
    }

    private string ElapsedText()
    {
        var started = Services.Recorder.StartedAt ?? DateTimeOffset.Now;
        var span = DateTimeOffset.Now - started;
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        return $"{(int)span.TotalMinutes:00}:{span.Seconds:00}";
    }

    protected override Control Build()
    {
        var cfg = Services.Config.Current.Capture;
        var root = Stack();

        if (Services.Recorder.IsRecording) root.Children.Add(RecordingCard());
        else
        {
            root.Children.Add(PanelKit.TileGrid(Palette,
                ("capture", "区域截图", () => Services.Dispatch("capture")),
                ("annotate", "批注", () => Services.Dispatch("annotate")),
                ("longshot", "长截图", () => Services.Dispatch("longshot")),
                ("record", "录屏", () =>
                {
                    Services.Dispatch("record");
                    // 录制启动有异步开销，稍后再刷新成计时态
                    global::Avalonia.Threading.DispatcherTimer.RunOnce(Refresh, TimeSpan.FromMilliseconds(600));
                }
            )));
        }

        root.Children.Add(PanelKit.Hint($"保存格式 {cfg.Format.ToUpperInvariant()} · 目录 {Shorten(cfg.Dir)}", Palette));

        var recent = LoadRecent();
        root.Children.Add(PanelKit.Divider(Palette));
        root.Children.Add(PanelKit.Section("最近文件", Palette));
        if (recent.Count == 0)
        {
            root.Children.Add(PanelKit.Hint("暂无截图记录，按上面任意按钮开始。", Palette));
        }
        else
        {
            foreach (var f in recent)
            {
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                row.Children.Add(new TextBlock
                {
                    Text = Path.GetFileName(f.FullName),
                    FontSize = 12.5,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = PanelKit.Body("", Palette).Foreground,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                var time = new TextBlock
                {
                    Text = f.LastWriteTime.ToString("MM-dd HH:mm"),
                    FontSize = 11.5,
                    Foreground = Palette.MutedBrush,
                    Margin = new Thickness(8, 0, 0, 0),
                };
                Grid.SetColumn(time, 1);
                row.Children.Add(time);

                var btn = new Button
                {
                    Content = row,
                    Padding = new Thickness(10, 8),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                };
                var path = f.FullName;
                btn.Click += (_, _) => Services.Shell.RevealInExplorer(path);
                root.Children.Add(btn);
            }
        }
        return root;
    }

    /// <summary>录制中状态卡（v1.2 recording-status: 红点 + 计时 + 停止按钮）。</summary>
    private Control RecordingCard()
    {
        _elapsed = new TextBlock
        {
            Text = ElapsedText(),
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(AppTheme.Rec),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var head = PanelKit.Row(8, PanelKit.Dot(AppTheme.Rec, 10), _elapsed);
        var stop = PanelKit.Flat("停止录屏", Palette, () =>
        {
            Services.Dispatch("record");
            global::Avalonia.Threading.DispatcherTimer.RunOnce(Refresh, TimeSpan.FromMilliseconds(900));
        }, primary: true);

        var body = Stack(10);
        body.Children.Add(head);
        body.Children.Add(stop);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x14, AppTheme.Rec.R, AppTheme.Rec.G, AppTheme.Rec.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x33, AppTheme.Rec.R, AppTheme.Rec.G, AppTheme.Rec.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(14),
            Child = body,
        };
    }

    private static string Shorten(string dir)
    {
        var expanded = Environment.ExpandEnvironmentVariables(dir);
        return expanded.Length > 34 ? "…" + expanded[^33..] : expanded;
    }

    private List<FileInfo> LoadRecent()
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
}

// ------------------------------------------------------------------ USB
internal sealed class UsbPanel(SidebarServices s, Palette p) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var enabled = Services.Config.Current.Usb.Enabled;
        var root = Stack(12);

        var status = PanelKit.Badge(enabled ? "运行中" : "已停止",
            enabled ? AppTheme.Ok : Palette.Muted,
            enabled ? Color.FromArgb(0x1F, AppTheme.Ok.R, AppTheme.Ok.G, AppTheme.Ok.B) : Palette.Hover);
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
        head.Children.Add(new TextBlock
        {
            Text = "USB 监控状态",
            FontSize = 13,
            Foreground = PanelKit.Body("", Palette).Foreground,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(status, 1);
        head.Children.Add(status);
        var refresh = PanelKit.Flat("刷新", Palette, Refresh);
        refresh.Margin = new Thickness(8, 0, 0, 0);
        Grid.SetColumn(refresh, 2);
        head.Children.Add(refresh);
        root.Children.Add(head);

        IReadOnlyList<UsbDriveInfo> drives;
        try { drives = Services.Usb.ListRemovable(); }
        catch { drives = []; }

        if (drives.Count == 0)
        {
            root.Children.Add(PanelKit.EmptyState("usb", "暂无 USB 设备连接",
                "插入 U 盘后会自动出现在这里，也可点“刷新”手动检测。", Palette));
        }
        else
        {
            root.Children.Add(PanelKit.Section($"已连接设备（{drives.Count}）", Palette));
            foreach (var d in drives)
            {
                var letter = new TextBlock
                {
                    Text = d.Drive,
                    FontSize = 14,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Palette.AccentBrush,
                };
                var label = PanelKit.Hint(string.IsNullOrWhiteSpace(d.Label) ? "USB 存储设备" : d.Label, Palette);
                // 空读卡器/无介质时 SizeGb 为 0，不要显示成 "0 GB"（看起来像故障）
                var capacity = d.SizeGb > 0 ? $"{d.SizeGb:0.#} GB" : "容量未知";
                var size = PanelKit.Hint($"{capacity} · {(d.Removable ? "可移动磁盘" : "固定磁盘")}", Palette);
                var info = Stack(2);
                info.Children.Add(letter);
                info.Children.Add(label);
                info.Children.Add(size);

                var open = PanelKit.Flat("打开", Palette, () =>
                {
                    try { Services.Shell.OpenPath(d.Drive); }
                    catch (Exception ex) { ToastService.Instance.Show($"打开 {d.Drive} 失败: {ex.Message}", "error"); }
                });

                var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
                grid.Children.Add(PanelKit.Icon("usb", Palette, 20));
                Grid.SetColumn(info, 1);
                info.Margin = new Thickness(10, 0, 8, 0);
                grid.Children.Add(info);
                Grid.SetColumn(open, 2);
                grid.Children.Add(open);
                root.Children.Add(PanelKit.Card(grid, Palette, pad: 10));
            }
        }
        return root;
    }
}

// ------------------------------------------------------------------ 打印机
internal sealed class PrinterPanel(SidebarServices s, Palette p) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var root = Stack(12);
        var head = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        head.Children.Add(new TextBlock
        {
            Text = "打印机状态",
            FontSize = 13,
            Foreground = PanelKit.Body("", Palette).Foreground,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var refresh = PanelKit.Flat("刷新", Palette, Refresh);
        Grid.SetColumn(refresh, 1);
        head.Children.Add(refresh);
        root.Children.Add(head);

        IReadOnlyList<PrinterInfo> printers;
        try { printers = Services.Printer.Query(); }
        catch { printers = []; }

        if (printers.Count == 0)
        {
            root.Children.Add(PanelKit.EmptyState("printer", "未检测到打印机",
                "连接打印机后点“刷新”重新检测。", Palette));
            return root;
        }

        foreach (var pr in printers)
        {
            var (text, color) = Describe(pr.State);
            var info = Stack(3);
            info.Children.Add(PanelKit.Body(pr.Name, Palette, 12.5, true));
            info.Children.Add(PanelKit.Row(6,
                PanelKit.Dot(color),
                PanelKit.Hint(text, Palette)));

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            grid.Children.Add(PanelKit.Icon("printer", Palette, 20));
            Grid.SetColumn(info, 1);
            info.Margin = new Thickness(10, 0, 0, 0);
            grid.Children.Add(info);
            root.Children.Add(PanelKit.Card(grid, Palette, pad: 10));
        }
        return root;
    }

    private static (string Text, Color Color) Describe(PrinterState state) => state switch
    {
        PrinterState.Ok => ("就绪", AppTheme.Ok),
        PrinterState.Idle => ("空闲", AppTheme.Ok),
        PrinterState.OutOfPaper => ("缺纸", AppTheme.Warn),
        PrinterState.LowPaper => ("纸张不足", AppTheme.Warn),
        PrinterState.LowInk => ("墨水不足", AppTheme.Warn),
        PrinterState.Jammed => ("卡纸", AppTheme.Err),
        PrinterState.Offline => ("离线", AppTheme.Err),
        PrinterState.DoorOpen => ("舱门未关", AppTheme.Err),
        PrinterState.ServiceRequired => ("需要维护", AppTheme.Err),
        _ => ("状态未知", AppTheme.Warn),
    };
}

// ------------------------------------------------------------------ 定时提醒
internal sealed class ReminderPanel(SidebarServices s, Palette p) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var root = Stack(10);
        root.Children.Add(PanelKit.Flat("＋ 新建提醒", Palette, () => Services.Dispatch("reminder"), primary: true));

        IReadOnlyList<Reminder> items;
        try { items = Services.Scheduler.ListReminders(); }
        catch { items = []; }

        if (items.Count == 0)
        {
            root.Children.Add(PanelKit.EmptyState("bell", "还没有提醒",
                "可以设置一次性、间隔或整点提醒，到点会在右下角弹出提示。", Palette));
            // 空列表时也要能调铃声 —— 提醒还没建的时候正是最该先设好铃声的时机
            root.Children.Add(PanelKit.Divider(Palette));
            root.Children.Add(BuildSoundCard());
            return root;
        }

        root.Children.Add(PanelKit.Section($"待触发（{items.Count}）", Palette));
        foreach (var r in items.OrderBy(x => x.At))
        {
            var when = DateTimeOffset.FromUnixTimeMilliseconds(r.At).ToLocalTime();
            var info = Stack(3);
            info.Children.Add(PanelKit.Body(string.IsNullOrWhiteSpace(r.Note) ? "（无备注）" : r.Note, Palette, 12.5, true));
            info.Children.Add(PanelKit.Hint($"{when:MM-dd HH:mm} · {KindText(r.Kind, r.RepeatMin)}", Palette));

            var snooze = PanelKit.Flat("+10分", Palette, () =>
            {
                Services.Scheduler.Snooze(r.Id, 10);
                Refresh();
            });
            var del = PanelKit.Flat("删除", Palette, () =>
            {
                Services.Scheduler.RemoveReminder(r.Id);
                Refresh();
            });
            del.Margin = new Thickness(6, 0, 0, 0);

            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            grid.Children.Add(info);
            var actions = PanelKit.Row(0, snooze, del);
            Grid.SetColumn(actions, 1);
            actions.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(actions);
            root.Children.Add(PanelKit.Card(grid, Palette, pad: 10));
        }

        root.Children.Add(PanelKit.Divider(Palette));
        root.Children.Add(BuildSoundCard());
        return root;
    }

    /// <summary>
    /// 铃声设置（v1.2 sound-card 折叠区）：类型 / 音量 / 重复次数 / 自定义 MP3 / 试听。
    /// 对应配置 reminderSound.*（Core 里早已建模，此前没有任何入口）。
    /// </summary>
    private Control BuildSoundCard()
    {
        var cfg = Services.Config.Current.ReminderSound;
        var body = Stack(14);

        // ---- 铃声类型
        var presets = new (string Value, string Label)[]
        {
            ("default", "默认"), ("bell", "铃声"), ("chime", "提示"), ("alarm", "警报"),
        };
        var presetRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        foreach (var (value, label) in presets)
        {
            var active = string.Equals(cfg.Preset, value, StringComparison.OrdinalIgnoreCase);
            var btn = PanelKit.Flat(label, Palette, () =>
            {
                Services.Config.Set("reminderSound.preset", value);
                Refresh();
            }, primary: active);
            btn.Padding = new Thickness(12, 5);
            presetRow.Children.Add(btn);
        }
        var presetBlock = Stack(6);
        presetBlock.Children.Add(PanelKit.Hint("铃声类型", Palette));
        presetBlock.Children.Add(presetRow);
        body.Children.Add(presetBlock);

        // ---- 音量
        var volumeLabel = PanelKit.Hint($"音量 {Math.Round(cfg.Volume * 100)}%", Palette);
        var volume = new Slider { Minimum = 0, Maximum = 100, Value = Math.Round(cfg.Volume * 100), Height = 24 };
        volume.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty) return;
            var v = Math.Round(volume.Value);
            volumeLabel.Text = $"音量 {v}%";
            Services.Config.Set("reminderSound.volume", v / 100.0);
        };
        body.Children.Add(volumeLabel);
        body.Children.Add(volume);

        // ---- 重复次数
        var repeatLabel = PanelKit.Hint($"重复次数 {cfg.Repeat} 次", Palette);
        var repeat = new Slider { Minimum = 1, Maximum = 5, Value = cfg.Repeat, Height = 24, TickFrequency = 1, IsSnapToTickEnabled = true };
        repeat.PropertyChanged += (_, e) =>
        {
            if (e.Property != Slider.ValueProperty) return;
            var r = (int)Math.Round(repeat.Value);
            repeatLabel.Text = $"重复次数 {r} 次";
            Services.Config.Set("reminderSound.repeat", r);
        };
        body.Children.Add(repeatLabel);
        body.Children.Add(repeat);

        // ---- 自定义 MP3
        var mp3Block = Stack(6);
        var mp3Row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var pick = PanelKit.Flat(cfg.Mp3Path is null ? "选择 MP3 文件" : "更换 MP3", Palette, PickMp3);
        mp3Row.Children.Add(pick);
        if (!string.IsNullOrWhiteSpace(cfg.Mp3Path))
        {
            mp3Row.Children.Add(PanelKit.Flat("清除", Palette, () =>
            {
                Services.Config.Set("reminderSound.mp3Path", null);
                Refresh();
            }));
        }
        mp3Block.Children.Add(PanelKit.Hint("自定义铃声", Palette));
        mp3Block.Children.Add(mp3Row);
        if (!string.IsNullOrWhiteSpace(cfg.Mp3Path))
        {
            mp3Block.Children.Add(PanelKit.Hint(Path.GetFileName(cfg.Mp3Path), Palette));
        }
        body.Children.Add(mp3Block);

        // ---- 试听
        body.Children.Add(PanelKit.Flat("试听铃声", Palette, () =>
        {
            try
            {
                _ = Services.Sound.PlayReminderAsync(Services.Config.Current.ReminderSound);
            }
            catch (Exception ex)
            {
                ToastService.Instance.Show($"试听失败: {ex.Message}", "error");
            }
        }));

        // 自绘折叠区（不用 Expander）：Expander 的展开动画会与侧栏窗口的
        // AppBar 重排在同一帧竞争布局，实测是原生崩溃的高发点。
        var chevron = new TextBlock
        {
            Text = "▸",
            FontSize = 12,
            Foreground = Palette.MutedBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        var title = new TextBlock
        {
            Text = "铃声设置",
            FontSize = 13,
            Foreground = PanelKit.Body("", Palette).Foreground,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var headRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { title, chevron },
        };
        var bodyHost = new Border
        {
            IsVisible = false,
            Padding = new Thickness(0, 12, 0, 4),
            Child = body,
        };
        var header = new Button
        {
            Content = headRow,
            Padding = new Thickness(10, 8),
            Margin = new Thickness(0, 2, 0, 0),
            Background = Palette.HoverBrush,
            BorderThickness = new Thickness(1),
            BorderBrush = Palette.DividerBrush,
            CornerRadius = new CornerRadius(10),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
        };
        header.Click += (_, _) =>
        {
            bodyHost.IsVisible = !bodyHost.IsVisible;
            chevron.Text = bodyHost.IsVisible ? "▾" : "▸";
        };

        var wrap = Stack(6);
        wrap.Children.Add(header);
        wrap.Children.Add(bodyHost);
        return wrap;
    }

    private async void PickMp3()
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top?.StorageProvider is not { } provider) return;
            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择提醒铃声",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("音频文件")
                    {
                        Patterns = ["*.mp3", "*.wav", "*.m4a", "*.wma"],
                    },
                ],
            });
            if (files.Count == 0) return;
            var path = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;
            Services.Config.Set("reminderSound.mp3Path", path);
            ToastService.Instance.Show("铃声已更新，点“试听铃声”确认", "success");
            Refresh();
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"选择铃声失败: {ex.Message}", "error");
        }
    }

    private static string KindText(ReminderKind kind, int? repeatMin) => kind switch
    {
        ReminderKind.Once => "一次性",
        ReminderKind.Interval => $"每 {repeatMin ?? 0} 分钟",
        ReminderKind.Hourly => "整点提醒",
        _ => "提醒",
    };
}

// ------------------------------------------------------------------ 快捷链接
internal sealed class LinksPanel(SidebarServices s, Palette p, IReadOnlyList<LinkRow> rows) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var root = Stack(6);
        if (rows.Count == 0)
        {
            root.Children.Add(PanelKit.EmptyState("link", "没有启用的链接",
                "在设置中开启需要的教学站点。", Palette));
            return root;
        }
        foreach (var row in rows)
        {
            var btn = new Button
            {
                Content = new TextBlock
                {
                    Text = row.Name,
                    FontSize = 13,
                    Foreground = Palette.AccentBrush,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                Command = row.OpenCommand,
                Padding = new Thickness(12, 10),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Background = Palette.HoverBrush,
                BorderThickness = new Thickness(1),
                BorderBrush = Palette.DividerBrush,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 0, 4),
            };
            root.Children.Add(btn);
        }
        return root;
    }
}

// ------------------------------------------------------------------ 任务管理器
internal sealed class TaskMgrPanel(SidebarServices s, Palette p) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var root = Stack(12);
        root.Children.Add(PanelKit.Hint("课堂投屏卡顿或应用无响应时，可在这里一键打开系统任务管理器结束占用。", Palette));
        root.Children.Add(PanelKit.Flat("打开任务管理器", Palette, () => Services.Dispatch("taskmgr"), primary: true));
        return root;
    }
}

// ------------------------------------------------------------------ 设置（快捷开关）
internal sealed class SettingsPanel(SidebarServices s, Palette p) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var cfg = Services.Config.Current;
        var root = Stack(14);

        root.Children.Add(PanelKit.Section("外观", Palette));
        var theme = new ComboBox
        {
            ItemsSource = new[] { "浅色（默认）", "深色" },
            SelectedIndex = string.Equals(cfg.Appearance.Theme, "dark", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        theme.SelectionChanged += (_, _) =>
        {
            var value = theme.SelectedIndex == 1 ? "dark" : "light";
            Services.Config.Set("appearance.theme", value);
            (TopLevel.GetTopLevel(this) as SidebarWindow)?.ApplyTheme();
        };
        root.Children.Add(theme);

        root.Children.Add(PanelKit.Section("启动", Palette));
        var autoLaunch = new CheckBox
        {
            Content = "开机自动启动",
            IsChecked = Services.Shell.GetAutoLaunch(),
            FontSize = 13,
        };
        autoLaunch.IsCheckedChanged += (_, _) =>
        {
            try { Services.Shell.SetAutoLaunch(autoLaunch.IsChecked == true); }
            catch (Exception ex) { ToastService.Instance.Show($"设置自启动失败: {ex.Message}", "error"); }
        };
        root.Children.Add(autoLaunch);

        root.Children.Add(PanelKit.Divider(Palette));
        root.Children.Add(PanelKit.Flat("打开完整设置…", Palette, () => Services.Dispatch("settings"), primary: true));
        root.Children.Add(PanelKit.Hint("热键、截图格式、录屏帧率、侧栏停靠边等高级选项在完整设置中。", Palette));
        return root;
    }
}
