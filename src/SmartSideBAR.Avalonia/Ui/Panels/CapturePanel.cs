// CapturePanel —— v1.2 同名面板的 Avalonia 实现。
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
                }),
                ("sidebar", "板中板", () => Services.Dispatch("miniboard"))
            ));
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
