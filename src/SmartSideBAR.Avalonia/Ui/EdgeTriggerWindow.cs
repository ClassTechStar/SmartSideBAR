// 希沃式边缘触发条 (建议 #1): 屏幕边缘居中的 14×160 竖条, 5px 本体 + 内向箭头。
// 触屏/大屏防误触: 常驻却只有 14px 宽, 不点击/悬停绝不展开。
//   hoverToExpand=false → 必须点击展开 (对标希沃);
//   hoverToExpand=true  → 悬停 150ms 滑出 (程序员习惯, Seelen delay_to_show 同值)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using SmartSideBAR.Windows.AppBar;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class EdgeTriggerWindow : Window
{
    private readonly int _widthDip;
    private readonly int _hoverDelayMs;
    private const int HeightDip = 160;
    private readonly Action _expand;
    private readonly bool _hoverToExpand;
    private readonly AppBarEdge _side;
    private bool _fired;

    /// <param name="widthDip">触发条宽度 (display.triggerWidthDip, 8-40 钳制)。</param>
    /// <param name="hoverDelayMs">悬停展开驻留 (display.hoverExpandDelayMs, 0-800)。</param>
    public EdgeTriggerWindow(Action expand, bool hoverToExpand, AppBarEdge side = AppBarEdge.Right,
        int widthDip = 14, int hoverDelayMs = 150)
    {
        _expand = expand;
        _hoverToExpand = hoverToExpand;
        _side = side;
        _widthDip = Math.Clamp(widthDip, 8, 40);
        _hoverDelayMs = Math.Clamp(hoverDelayMs, 0, 800);

        Width = _widthDip;
        Height = HeightDip;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [];

        var accent = SystemAccent.Get();
        var right = _side == AppBarEdge.Right;
        var strip = new Border
        {
            Width = 5,
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = right ? global::Avalonia.Layout.HorizontalAlignment.Right : global::Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Stretch,
            // 品牌色 60% 透明度: 可发现但不喧宾夺主
            Background = new SolidColorBrush(Color.FromArgb(0x99, accent.R, accent.G, accent.B)),
        };
        // 内向箭头: 指向屏幕内侧, 暗示展开方向 (Path+StreamGeometry, 与 DockWindow 同法)
        var chevron = new global::Avalonia.Controls.Shapes.Path
        {
            Data = StreamGeometry.Parse(right ? "M 2 0 L 7 5 L 2 10" : "M 7 0 L 2 5 L 7 10"),
            Stroke = new SolidColorBrush(Colors.White),
            StrokeThickness = 1.6,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            HorizontalAlignment = right ? global::Avalonia.Layout.HorizontalAlignment.Right : global::Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(2),
            IsHitTestVisible = false,
        };
        var root = new Panel();
        root.Children.Add(strip);
        root.Children.Add(chevron);
        Content = root;

        // 悬停展开: 150ms 驻留, 移出即取消 (Seelen fancy-toolbar delay_to_show 同值)
        // 触屏防误触: 手指划过边缘不等于"想展开", 触屏指针不触发 hover (2026-11 审计)
        DispatcherTimer? dwell = null;
        if (_hoverToExpand)
        {
            PointerEntered += (_, e) =>
            {
                if (e.Pointer.Type == PointerType.Touch) return;
                if (dwell is null)
                {
                    dwell = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_hoverDelayMs) };
                    dwell.Tick += (_, _) =>
                    {
                        dwell!.Stop();
                        if (IsPointerOver && !_fired)
                        {
                            _fired = true;
                            _expand();
                        }
                    };
                }
                dwell.Stop(); // 只订阅一次; 每次进入都叠加 Tick = N 次/150ms
                dwell.Start();
            };
            PointerExited += (_, _) => dwell?.Stop();
        }

        // 点击展开 (两种模式都支持)
        Tapped += (_, _) =>
        {
            if (_fired) return;
            _fired = true;
            _expand();
        };
    }

    /// <summary>每次重新显示时重置一次性标记 (收起→展开可反复)。</summary>
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        _fired = false;
    }
}
