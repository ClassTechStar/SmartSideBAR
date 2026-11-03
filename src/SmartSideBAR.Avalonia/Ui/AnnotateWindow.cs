// 屏幕批注窗 (§6.3), P1-8 拆分后的窗口壳: 铺屏/底图/指针接线/保存。
// 引擎见 Ui/Ink/InkCanvas.cs, 工具条见 Ui/Ink/AnnotateToolbar.cs (与板中板共享)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Core.Telemetry;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Ink;
using SmartSideBAR.Windows.Native;
using AloLayout = global::Avalonia.Layout;

namespace SmartSideBAR.Avalonia.Ui;

/// <summary>批注主窗: 全屏透明置顶; 导出 StrokesOnly | WithBackground。</summary>
public sealed class AnnotateWindow : Window
{
    private readonly InkCanvas _ink = new();
    private readonly string _dir;
    private readonly string _format;
    private readonly byte[]? _backgroundPng; // 打开时的全屏底图 (WithBackground 用)
    private readonly IEventBus? _bus;
    private readonly InkTextCard _textCard = new();

    public AnnotateWindow(byte[]? backgroundPng, string dir, string format, bool inkToShape = true, IEventBus? bus = null)
    {
        _backgroundPng = backgroundPng;
        _bus = bus;
        OverlayGate.Enter();
        _dir = dir;
        _format = format;

        // B25  透明/无边框/置顶必须在 Show 之前设定 (分层窗口语义)。
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        CanResize = false;
        ShowActivated = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [];

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // B25: 以打开瞬间的屏幕快照作底, 大面积窗口的分层透明在 Win32 上不可靠。
        var backdrop = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
        if (_backgroundPng is not null)
        {
            try
            {
                using var bgStream = new MemoryStream(_backgroundPng);
                backdrop.Source = new Bitmap(bgStream);
            }
            catch
            {
                // 坏底图退化为深色幕布 —— 这里抛出会让 OverlayGate.Enter 永久泄漏
                // (ctor 之后无人调 Exit), 侧栏自动收起从此失效
                backdrop.Source = null;
                grid.Background = new SolidColorBrush(Color.FromArgb(255, 32, 38, 48));
            }
        }
        else
        {
            backdrop.Source = null;
            grid.Background = new SolidColorBrush(Color.FromArgb(255, 32, 38, 48));
        }
        Grid.SetRow(backdrop, 0);
        Grid.SetRowSpan(backdrop, 2);
        grid.Children.Add(backdrop);

        _ink.HorizontalAlignment = AloLayout.HorizontalAlignment.Stretch;
        _ink.VerticalAlignment = AloLayout.VerticalAlignment.Stretch;
        Grid.SetRow(_ink, 0);
        Grid.SetRowSpan(_ink, 2);
        grid.Children.Add(_ink);

        var inkHost = new Border
        {
            Background = Brushes.Transparent,
            HorizontalAlignment = AloLayout.HorizontalAlignment.Stretch,
            VerticalAlignment = AloLayout.VerticalAlignment.Stretch,
        };
        inkHost.PointerPressed += (_, e) =>
        {
            if (_ink.HasActiveStroke) return; // 多指/掌压保护: 活动笔画进行中, 第二触点不得覆盖
            var p = e.GetCurrentPoint(inkHost);
            if (!p.Properties.IsLeftButtonPressed) return;
            _ink.BeginStroke(p.Position, e.Pointer, inkHost); // 捕获到 inkHost 自身 (B5)
            e.Handled = true;
        };
        inkHost.PointerMoved += (_, e) =>
        {
            if (e.Pointer.Captured != inkHost) return;
            _ink.ExtendStroke(e.GetCurrentPoint(inkHost).Position);
            e.Handled = true;
        };
        inkHost.PointerReleased += (_, e) =>
        {
            if (e.Pointer.Captured != inkHost) return;
            _ink.EndStroke();
            e.Pointer.Capture(null);
            e.Handled = true;
        };
        // 系统抢走捕获 (无 Released) 时收尾, 否则活动笔画悬挂
        inkHost.PointerCaptureLost += (_, _) => _ink.EndStroke();
        Grid.SetRow(inkHost, 0);
        Grid.SetRowSpan(inkHost, 2);
        grid.Children.Add(inkHost);

        // 「文」工具: 收笔 → 本组笔画白底渲染 → 离线 OCR → 浮层卡片 (一键复制)
        _ink.TextInkSubmitted += async (_, stroke) =>
        {
            try
            {
                var scale = Win32Display.GetScaling(TryGetPlatformHandle()?.Handle ?? 0);
                if (scale <= 0) scale = 1.0;
                _textCard.Show("识别中…");
                using var bmp = _ink.ExportStrokeGroup([stroke], scale);
                using var ms = new MemoryStream();
                bmp.Save(ms);
                var result = await InkTextService.RecognizeAsync(ms.ToArray());
                if (result is null)
                {
                    _textCard.Show("本机未安装可用的 OCR 语言包（设置 → 时间和语言 → 语言 → 可选功能）");
                    return;
                }
                if (string.IsNullOrWhiteSpace(result.Text))
                {
                    _textCard.Show("未识别出文字 —— 请写大一点、笔画分开一点");
                    return;
                }
                _textCard.Show(result.Text);
                _bus?.Publish(new SmartSideBAR.Core.Telemetry.StatsRecorded("annotate.ocr", 1, result.Text));
            }
            catch (Exception ex)
            {
                _textCard.Show($"识别失败: {ex.Message}");
            }
        };
        _textCard.HorizontalAlignment = AloLayout.HorizontalAlignment.Center;
        _textCard.VerticalAlignment = AloLayout.VerticalAlignment.Top;
        _textCard.Margin = new Thickness(0, 64, 0, 0);
        Grid.SetRow(_textCard, 0);
        Grid.SetRowSpan(_textCard, 2);
        grid.Children.Add(_textCard);

        var toolbar = new AnnotateToolbar(_ink,
            save: () => Save(withBackground: true),
            close: Close,
            saveInkOnly: () => Save(withBackground: false),
            defaultColorIndex: 1,           // 默认蓝 (v1.2 语义)
            inkToShape: inkToShape);
        var toolbarHost = new StackPanel
        {
            Orientation = AloLayout.Orientation.Horizontal,
            HorizontalAlignment = AloLayout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0),
            Children = { toolbar.Host },
        };
        Grid.SetRow(toolbarHost, 1);
        grid.Children.Add(toolbarHost);

        Content = grid;

        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) Close();
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        OverlayGate.Exit();
        base.OnClosed(e);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        FillPrimaryMonitor();
    }

    /// <summary>铺满主屏 (物理像素 → DIP)。用显式几何替代 FullScreen, 保住分层透明。</summary>
    private void FillPrimaryMonitor()
    {
        var (x, y, w, h) = Win32Display.PrimaryMonitorRectPx();
        var scale = Win32Display.GetScaling(TryGetPlatformHandle()?.Handle ?? 0);
        if (scale <= 0) scale = 1.0;
        Position = new PixelPoint(x, y);
        Width = w / scale;
        Height = h / scale;
    }

    private void Save(bool withBackground)
    {
        try
        {
            var path = Path.Combine(_dir, $"annotate_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            Directory.CreateDirectory(_dir);
            var scale = Win32Display.GetScaling(TryGetPlatformHandle()?.Handle ?? 0);
            var w = (int)(Bounds.Width * scale);
            var h = (int)(Bounds.Height * scale);

            if (withBackground && _backgroundPng is not null)
            {
                using var bg = new Bitmap(new MemoryStream(_backgroundPng));
                using var strokes = _ink.ExportStrokesOnly(w, h, scale);
                using var composite = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96, 96));
                using (var ctx = composite.CreateDrawingContext())
                {
                    ctx.DrawImage(bg, new Rect(0, 0, w, h));
                    ctx.DrawImage(strokes, new Rect(0, 0, w, h));
                }
                composite.Save(path);
            }
            else
            {
                using var strokes = _ink.ExportStrokesOnly(w, h, scale);
                strokes.Save(path);
            }
            ToastService.Instance.Show($"批注已保存: {path}", "success");
            _bus?.Publish(new SmartSideBAR.Core.Telemetry.StatsRecorded("annotate.save", 1, $"{_ink.Count} 笔"));
            Close();
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"批注保存失败: {ex.Message}", "error");
        }
    }
}
