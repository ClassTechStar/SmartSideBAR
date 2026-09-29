// 轻量进程内 Toast (右下角, 3.5s 自动消失) —— 替代 Electron renderer 通知。
// C4-③: 加队列与去重 —— 相同消息 3s 内不重复弹出; 同时最多显示 3 条, 超出排队。
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class ToastService
{
    public static ToastService Instance { get; } = new();

    private const int MaxVisible = 3;
    private const int DedupWindowMs = 3000;

    private readonly Queue<(string Message, string Tone)> _pending = new();
    private readonly Dictionary<string, long> _recentShown = new();
    private int _visibleCount;

    public void Show(string message, string tone = "info")
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 去重: 相同消息在 DedupWindowMs 内不重复弹出
            var now = Environment.TickCount64;
            if (_recentShown.TryGetValue(message, out var lastShown) && now - lastShown < DedupWindowMs)
                return;
            _recentShown[message] = now;

            // 清理过期去重记录
            if (_recentShown.Count > 50)
            {
                var expired = _recentShown.Where(kv => now - kv.Value >= DedupWindowMs).Select(kv => kv.Key).ToList();
                foreach (var key in expired) _recentShown.Remove(key);
            }

            if (_visibleCount >= MaxVisible)
            {
                _pending.Enqueue((message, tone));
                return;
            }

            ShowCore(message, tone);
        });
    }

    private void ShowCore(string message, string tone)
    {
        try
        {
            _visibleCount++;
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
            try
            {
                var screen = win.Screens.Primary ?? win.Screens.All[0];
                var yOffset = 140 + _visibleCount * 60;
                win.Position = new PixelPoint(
                    screen.Bounds.Right - 420,
                    screen.Bounds.Bottom - yOffset);
            }
            catch { /* 定位尽力而为 */ }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3500) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                win.Close();
                _visibleCount = Math.Max(0, _visibleCount - 1);
                // 从队列取下一条
                if (_pending.Count > 0)
                {
                    var next = _pending.Dequeue();
                    ShowCore(next.Message, next.Tone);
                }
            };
            timer.Start();
        }
        catch
        {
            _visibleCount = Math.Max(0, _visibleCount - 1);
        }
    }
}
