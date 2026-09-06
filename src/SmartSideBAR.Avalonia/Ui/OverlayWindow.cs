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
        _dir = dir;
        _format = format;
    }

    protected override void OnOpened(EventArgs e)
    {
        Topmost = true;
        WindowState = WindowState.FullScreen;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;

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
        Content = new Panel
        {
            Background = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
            Children = { canvas, hint },
        };
        KeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape) Close();
            else if (args.Key == Key.Enter) FinishSelection();
        };
        base.OnOpened(e);
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
    private void FinishSelection()
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
        Close();

        Task.Run(() =>
        {
            var bmp = _capture.CaptureRegion(region);
            if (bmp is null)
            {
                ToastService.Instance.Show("截图失败", "error");
                return;
            }
            var path = _capture.SaveImage(bmp, _dir, _format);
            bmp.Dispose();
            ToastService.Instance.Show($"截图已保存: {path}", "success");
        });
    }
}
