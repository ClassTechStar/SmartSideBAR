// 区域截图覆盖层 (§6.2): 全屏透明置顶 → 拖拽选框 (Pointer 统一鼠标/触屏) → BitBlt 物理裁剪。
// Esc 取消 / Enter 或松手确认; 坐标 DIP↔物理仅在此边界换算一次 (K4)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Capture;
using SmartSideBAR.Windows.Native;
using AloLayout = global::Avalonia.Layout;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class OverlayWindow : Window
{
    private readonly ICaptureApi _capture;
    private readonly string _dir;
    private readonly string _format;
    private Point _start;
    private Border? _selection;
    private bool _dragging;
    private bool _done;

    public OverlayWindow(ICaptureApi capture, string dir, string format)
    {
        _capture = capture;
        OverlayGate.Enter();
        _dir = dir;
        _format = format;

        // B25 同类根修: 透明底必须在 Show 之前生效, 否则窗口按主题白底创建,
        // 遮罩层叠在白底上 → 用户看到的是"灰白幕布"而不是被压暗的桌面。
        // 另: 不用 WindowState.FullScreen —— 该状态会丢掉分层透明, 改为显式铺满显示器。
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        CanResize = false;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [];

        // B25 根修: 不再依赖"窗口分层透明"来透出桌面 —— 大面积窗口在 Avalonia/Win32 下
        // 会退化为不透明表面 (实测整屏纯白, 用户看不见要截什么)。
        // 改为: 打开瞬间抓一张显示器快照作为底图, 用户看到的就是即将被截下的画面。
        _selection = new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#2B6EE0")),
            BorderThickness = new Thickness(2),
            Background = new SolidColorBrush(Color.FromArgb(40, 43, 110, 224)),
            IsVisible = false,
        };
        var canvas = new Canvas { Children = { _selection } };
        var hint = new TextBlock
        {
            Text = "拖拽选择截图区域 · Enter 确认 · Esc 取消",
            Foreground = Brushes.White,
            FontSize = 15,
            HorizontalAlignment = AloLayout.HorizontalAlignment.Center,
            VerticalAlignment = AloLayout.VerticalAlignment.Top,
            Margin = new Thickness(0, 24, 0, 0),
        };
        var stack = new Panel();
        if (SnapshotBackground() is { } shot)
        {
            stack.Children.Add(new Image { Source = shot, Stretch = Stretch.Fill, IsHitTestVisible = false });
            // 压暗一层, 让选框与提示更清晰 (对应原 #3C000000 遮罩)
            stack.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), IsHitTestVisible = false });
        }
        else
        {
            stack.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(230, 20, 26, 34)), IsHitTestVisible = false });
        }
        stack.Children.Add(canvas);
        stack.Children.Add(hint);
        Content = stack;

        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) Close();
            else if (args.Key == Key.Enter) FinishSelection();
        };
    }

    /// <summary>当前主屏快照 (物理像素) → Avalonia 位图; 失败返回 null。</summary>
    private global::Avalonia.Media.Imaging.Bitmap? SnapshotBackground()
    {
        try
        {
            using var bmp = _capture.CaptureMonitor(0);
            if (bmp is null) return null;
            using var ms = new MemoryStream();
            bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            return new global::Avalonia.Media.Imaging.Bitmap(ms);
        }
        catch
        {
            return null;
        }
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

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var p = e.GetCurrentPoint(this);
        if (!p.Properties.IsLeftButtonPressed) return;
        _dragging = true;
        _start = p.Position;
        _selection!.IsVisible = true;
        Canvas.SetLeft(_selection!, _start.X);
        Canvas.SetTop(_selection!, _start.Y);
        _selection!.Width = 0;
        _selection!.Height = 0;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_dragging) return;
        var p = e.GetCurrentPoint(this).Position;
        var x = Math.Min(_start.X, p.X);
        var y = Math.Min(_start.Y, p.Y);
        _selection!.Width = Math.Abs(p.X - _start.X);
        _selection!.Height = Math.Abs(p.Y - _start.Y);
        Canvas.SetLeft(_selection!, x);
        Canvas.SetTop(_selection!, y);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        FinishSelection();
        e.Handled = true;
    }

    /// <summary>选框 → 物理像素 → BitBlt 裁剪 → 落盘 → 通知。</summary>
    private async void FinishSelection()
    {
        if (_done || _selection is not { } sel || sel.Width < 8 || sel.Height < 8)
        {
            Close();
            return;
        }
        _done = true;
        var left = Canvas.GetLeft(sel);
        var top = Canvas.GetTop(sel);
        var scaling = Win32Display.GetScaling(TryGetPlatformHandle()?.Handle ?? 0);
        var region = new AppBarRect(
            (int)Math.Round(left * scaling),
            (int)Math.Round(top * scaling),
            (int)Math.Round(sel.Width * scaling),
            (int)Math.Round(sel.Height * scaling));

        // B10 根修: Close() 只发起销毁, DWM 合成器尚未把遮罩层从画面摘掉;
        // 立刻 BitBlt 会把 #3C000000 遮罩与选框一起截进图里。先隐藏窗口, 等一帧再抓。
        Hide();
        await Task.Delay(160);
        Close();

        try
        {
            var saved = await Task.Run(() =>
            {
                var bmp = _capture.CaptureRegion(region);
                if (bmp is null) return null;
                try
                {
                    return _capture.SaveImage(bmp, _dir, _format);
                }
                finally
                {
                    bmp.Dispose();
                }
            });
            ToastService.Instance.Show(saved is null ? "截图失败" : $"截图已保存: {saved}",
                saved is null ? "error" : "success");
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"截图失败: {ex.Message}", "error");
        }
    }
}
