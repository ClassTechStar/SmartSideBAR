// 分页滑动转场 (P2-3)。
// 借鉴 DiffiTouch 的 finish_slide_animation：当前页与"影子页"两条位移动画并行，
// 150–180ms、缓出；关闭动效时直接切换，不做动画。
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Ui;

public static class PageTransition
{
    /// <summary>默认转场时长（毫秒）。取面板动效令牌，无令牌时按 180ms。</summary>
    public static int DurationMs
    {
        get
        {
            var ms = DesignTokens.Current.Motion.PanelMs;
            return ms > 0 ? (int)ms : 180;
        }
    }

    /// <summary>
    /// 横向滑动转场：current 滑出、next 滑入。direction &gt; 0 表示下一页（向左）。
    /// </summary>
    public static async Task SlideAsync(Control current, Control next, double width, int direction, bool animate = true)
    {
        if (!animate)
        {
            current.RenderTransform = null;
            next.RenderTransform = null;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(DurationMs);
        var outTo = new TranslateTransform(direction > 0 ? -width : width, 0);
        var inFrom = new TranslateTransform(direction > 0 ? width : -width, 0);

        current.RenderTransform = new TranslateTransform(0, 0);
        next.RenderTransform = inFrom;

        var outAnim = new Animation
        {
            Duration = duration,
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.XProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, outTo.X) } },
            },
        };
        var inAnim = new Animation
        {
            Duration = duration,
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(TranslateTransform.XProperty, inFrom.X) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(TranslateTransform.XProperty, 0d) } },
            },
        };

        _ = outAnim.RunAsync(current);
        await inAnim.RunAsync(next);

        current.RenderTransform = null;
        next.RenderTransform = null;
    }
}
