// 轻量进程内 Toast (右下角, 3.5s 自动消失), 替代 Electron renderer 通知。
// 动效: 滑入 (translateY 14→0 + fade, 200ms easeOut) / 淡出 150ms / 点击即消失。
//
// P0-2: 队列换成带优先级的 NotificationQueue (借鉴 ClassIsland 的 PriorityQueue 通知机制
// 与 Class-Widgets-2 的来源/级别模型)。对外 Show(message, tone) 签名不变, 调用方零改动。
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using Avalonia.Threading;
using SmartSideBAR.Core.Messaging;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class ToastService
{
    public static ToastService Instance { get; } = new();

    private const int MaxVisible = 3;
    private const int DedupWindowMs = 3000;

    /// <summary>来源优先级: 越靠前越优先出队。</summary>
    private static readonly string[] SourceOrder = ["app", "reminder", "usb", "recorder", "printer"];

    private readonly NotificationQueue _queue = new(SourceRank, capacity: 50);
    private readonly Dictionary<string, long> _recentShown = new();
    private int _visibleCount;

    private static int SourceRank(string source)
    {
        var i = System.Array.IndexOf(SourceOrder, source);
        return i < 0 ? SourceOrder.Length : i;
    }

    /// <summary>原有入口: 按语气字符串弹一条。</summary>
    public void Show(string message, string tone = "info") => Show("app", ToLevel(tone), message);

    /// <summary>带来源与级别的入口。</summary>
    public void Show(string source, NotificationLevel level, string message, bool priorityOverride = false)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 去重: 相同消息在 DedupWindowMs 内不重复弹出
            var now = Environment.TickCount64;
            if (_recentShown.TryGetValue(message, out var lastShown) && now - lastShown < DedupWindowMs)
                return;
            _recentShown[message] = now;

            // 通知中心留档（提醒历史/USB/录屏等事件统一可回看）
            NotificationCenter.Instance.Push(message, ToTone(level));

            // 清理过期去重记录
            if (_recentShown.Count > 50)
            {
                var expired = _recentShown.Where(kv => now - kv.Value >= DedupWindowMs).Select(kv => kv.Key).ToList();
                foreach (var key in expired) _recentShown.Remove(key);
            }

            _queue.Enqueue(source, message, level, priorityOverride);
            Pump();
        });
    }

    /// <summary>按优先级取出可显示的通知, 直到占满可视配额。</summary>
    private void Pump()
    {
        while (_visibleCount < MaxVisible)
        {
            var next = _queue.Dequeue();
            if (next is null) return;
            ShowCore(next);
        }
    }

    /// <summary>当前排队情况(设置窗/诊断可看)。</summary>
    public IReadOnlyList<NotificationRequest> Pending => _queue.Snapshot();

    private static NotificationLevel ToLevel(string tone) => tone switch
    {
        "success" => NotificationLevel.Success,
        "warn" => NotificationLevel.Warn,
        "error" => NotificationLevel.Error,
        _ => NotificationLevel.Info,
    };

    private static string ToTone(NotificationLevel level) => level switch
    {
        NotificationLevel.Success => "success",
        NotificationLevel.Warn => "warn",
        NotificationLevel.Error => "error",
        _ => "info",
    };

    private void ShowCore(NotificationRequest req)
    {
        try
        {
            _visibleCount++;
            var color = req.Level switch
            {
                NotificationLevel.Success => "#2E7D32",
                NotificationLevel.Warn => "#B26A00",
                NotificationLevel.Error => "#C62828",
                _ => "#243041",
            };
            var text = new TextBlock
            {
                Text = req.Message,
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
                // 释放配额后继续按优先级取
                Pump();
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
