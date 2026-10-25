// ReminderPanel —— v1.2 同名面板的 Avalonia 实现。
// P2-1：由 697 行的 SidebarPanels.cs 拆出，一面板一文件（单文件 ≤600 行的拆分约定），
// 便于单独单测、减少合并冲突。公共构件见 PanelKit.cs，基类见 SidebarPanel.cs。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Devices;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.Devices;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed class ReminderPanel(SidebarServices s, Palette p) : SidebarPanel(s, p)
{
    private int _tab; // 0=提醒 1=通知（Seelen 通知中心页签）
    private bool _hooked;
    private Action? _onNotificationsChanged;

    protected override Control Build()
    {
        var root = Stack(10);
        root.Children.Add(BuildTabs());
        root.Children.Add(_tab == 1 ? BuildNotifications() : BuildReminders());
        return root;
    }

    protected override void OnBuilt()
    {
        // 通知有更新时刷新通知页签（只挂一次; 面板脱离可视树时摘除）
        if (_hooked) return;
        _hooked = true;
        _onNotificationsChanged = () => global::Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);
        NotificationCenter.Instance.Changed += _onNotificationsChanged;
    }

    protected override void OnDetachedFromVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_onNotificationsChanged is not null)
        {
            NotificationCenter.Instance.Changed -= _onNotificationsChanged;
            _hooked = false;
        }
    }

    /// <summary>页签（Seelen segmented 语法: 选中 = accent 实心）。</summary>
    private Control BuildTabs()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        string[] tabs = ["提醒", "通知"];
        for (var i = 0; i < tabs.Length; i++)
        {
            var index = i;
            var btn = PanelKit.Flat(tabs[i], Palette, () =>
            {
                if (_tab == index) return;
                _tab = index;
                Refresh();
            }, primary: _tab == index);
            btn.Padding = new Thickness(16, 6);
            row.Children.Add(btn);
        }
        return row;
    }

    /// <summary>通知中心页 —— Seelen notifications 卡片语法: 计数 + 全部清除 + 卡片列表。</summary>
    private Control BuildNotifications()
    {
        var center = NotificationCenter.Instance;
        var root = Stack(8);

        var headRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        headRow.Children.Add(PanelKit.Section($"通知动态（{center.Items.Count}）", Palette));
        var clear = PanelKit.Flat("全部清除", Palette, () => center.Clear());
        Grid.SetColumn(clear, 1);
        clear.VerticalAlignment = VerticalAlignment.Center;
        headRow.Children.Add(clear);
        root.Children.Add(headRow);

        if (center.Items.Count == 0)
        {
            root.Children.Add(PanelKit.EmptyState("bell", "暂无通知",
                "提醒到点、U 盘插拔、录屏完成等事件都会在这里留下一条动态。", Palette));
            return root;
        }

        foreach (var n in center.Items)
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 8 };
            grid.Children.Add(PanelKit.Dot(n.Tone switch
            {
                "success" => AppTheme.Ok,
                "warn" => AppTheme.Warn,
                "error" => AppTheme.Err,
                _ => AppTheme.Ok,
            }, 8));
            var info = Stack(2);
            info.Children.Add(PanelKit.Body(n.Message, Palette, 12.5));
            info.Children.Add(PanelKit.Hint(n.Time.ToString("MM-dd HH:mm"), Palette));
            Grid.SetColumn(info, 1);
            grid.Children.Add(info);
            var dismiss = new Button
            {
                Content = new TextBlock { Text = "×", FontSize = 14 },
                Width = 26,
                Height = 26,
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            var id = n.Id;
            dismiss.Click += (_, _) => center.Remove(id);
            Grid.SetColumn(dismiss, 2);
            grid.Children.Add(dismiss);

            var card = PanelKit.Card(grid, Palette, pad: 10);
            // 卡片按压反馈（Seelen notifications: scale 0.9 @100ms）
            card.Transitions = new global::Avalonia.Animation.Transitions
            {
                new global::Avalonia.Animation.TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(100),
                },
            };
            card.RenderTransform = global::Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");
            card.PointerPressed += (_, _) => card.RenderTransform = global::Avalonia.Media.Transformation.TransformOperations.Parse("scale(0.97)");
            card.PointerReleased += (_, _) => card.RenderTransform = global::Avalonia.Media.Transformation.TransformOperations.Parse("scale(1)");
            root.Children.Add(card);
        }
        root.Children.Add(PanelKit.Hint("最多保留 50 条，仅本机内存，不落盘。", Palette));
        return root;
    }

    private Control BuildReminders()
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
