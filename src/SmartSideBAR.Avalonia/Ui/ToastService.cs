// 轻量进程内 Toast (右下角, 3.5s 自动消失) —— 替代 Electron renderer 通知。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class ToastService
{
    public static ToastService Instance { get; } = new();

    public void Show(string message, string tone = "info")
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var color = tone switch
                {
                    "success" => "#2E7D32",
                    "warn" => "#B26A00",
                    "error" => "#C62828",
                    _ => "#243041",
                };
                var text = new TextBlock
                {
                    Text = message,
                    Foreground = Brushes.White,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 360,
                    FontSize = 13,
                };
                var border = new Border
                {
                    Background = new SolidColorBrush(Color.Parse(color)),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14, 10),
                    Child = text,
                    Opacity = 0.96,
                };
                var win = new Window
                {
                    SystemDecorations = SystemDecorations.None,
                    ShowInTaskbar = false,
                    TransparencyLevelHint = [],
                    Background = Brushes.Transparent,
                    SizeToContent = SizeToContent.WidthAndHeight,
                    ShowActivated = false,
                    Topmost = true,
                    Content = border,
                };
                win.Show();
                // 右下角 (Show 后取屏幕信息; DIP 边界 + 物理像素位置的混合由 Avalonia 折算)
                try
                {
                    var screen = win.Screens.Primary ?? win.Screens.All[0];
                    win.Position = new PixelPoint(
                        screen.Bounds.Right - 420,
                        screen.Bounds.Bottom - 140);
                }
                catch { /* 定位尽力而为 */ }
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3500) };
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    win.Close();
                };
                timer.Start();
            }
            catch
            {
                // 通知失败不影响业务
            }
        });
    }
}
