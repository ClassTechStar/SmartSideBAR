// 轻量进程内 Toast (右下角, 3.5s 自动消失), 替代 Electron renderer 通知。
// C4-③: 加队列与去重, 相同消息 3s 内不重复弹出; 同时最多显示 3 条, 超出排队。
// 动效: 滑入 (translateY 14→0 + fade, 200ms easeOut) / 淡出 150ms / 点击即消失。
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
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

            // 通知中心留档（提醒历史/USB/录屏等事件统一可回看）
            NotificationCenter.Instance.Push(message, tone);

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
                Opacity = 0,
            };
            var translate = new TranslateTransform(0, 14);
            border.RenderTransform = translate;
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

            PlaySlideIn(border, translate);

            var closed = false;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3500) };

            void OnClosed()
            {
                _visibleCount = Math.Max(0, _visibleCount - 1);
                // 从队列取下一条
                if (_pending.Count > 0)
                {
                    var next = _pending.Dequeue();
                    ShowCore(next.Message, next.Tone);
                }
            }

            void StartClose()
            {
                if (closed) return;
                closed = true;
                timer.Stop();
                PlayFadeOut(border);
                // 等淡出结束再关窗, 关闭后释放配额并取下一条
                DispatcherTimer.RunOnce(() =>
                {
                    win.Close();
                    OnClosed();
                }, TimeSpan.FromMilliseconds(150));
            }

            timer.Tick += (_, _) => StartClose();
            timer.Start();
                        // 点击即消失。教室场景里错误提示不该挡住操作 3.5s
            border.PointerPressed += (_, _) => StartClose();
        }
        catch
        {
            _visibleCount = Math.Max(0, _visibleCount - 1);
        }
    }

    private static void PlaySlideIn(Border border, TranslateTransform translate)
    {
        var appear = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 1d) } },
            },
        };
        _ = appear.RunAsync(border);
        var slide = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(200),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.YProperty, 14d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.YProperty, 0d) } },
            },
        };
        _ = slide.RunAsync(translate);
    }

    private static void PlayFadeOut(Border border)
    {
        var fade = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(150),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, border.Opacity) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 0d) } },
            },
        };
        _ = fade.RunAsync(border);
        // 淡出动画已启动, 关窗由调用方的 RunOnce 负责
    }
}
