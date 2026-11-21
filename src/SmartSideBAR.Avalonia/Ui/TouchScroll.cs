// 触屏拖动滚动 (P2-2)。
// 借鉴 DiffiTouch 的 TouchListView：列表按像素滚动、超过阈值才判定为拖动，
// 手指微动不会被当成点击，也不至于一碰就滚。Avalonia 自带手势，这里补的是
// 「拖动即滚 + 阈值」这一层，便于在触屏一体机上调到顺手。
//
// 关键 (审计修复): 按下落在内层按钮上时, ButtonBase 会捕获指针 —— 捕获不移走,
// ScrollViewer 收不到后续移动/释放, 拖不动还误触按钮。改为:
//   * 按下/移动/释放走隧道阶段监听 (handledEventsToo), 按钮拿到按下我们也看得到;
//   * 越过阈值瞬间把指针捕获抢到 viewer —— 按钮 LostCapture 取消按压态, Click 不再触发;
//   * 静止点击 (未越阈值) 不抢捕获, 按钮正常响应。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace SmartSideBAR.Avalonia.Ui;

public static class TouchScroll
{
    /// <summary>默认拖动判定阈值（DIP）。</summary>
    public const double DefaultThreshold = 10;

    /// <summary>为 ScrollViewer 接入「按住拖动即滚动」。</summary>
    public static void Attach(ScrollViewer viewer, double threshold = DefaultThreshold)
    {
        var state = new State { Viewer = viewer, Threshold = threshold };

        // 隧道 + handledEventsToo: 内层按钮会把 PointerPressed 标记为已处理,
        // 隧道阶段先于按钮本身执行, 拖动起点由此记录
        viewer.AddHandler(global::Avalonia.Input.InputElement.PointerPressedEvent, OnPressed,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        viewer.AddHandler(global::Avalonia.Input.InputElement.PointerMovedEvent, OnMoved,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        viewer.AddHandler(global::Avalonia.Input.InputElement.PointerReleasedEvent, OnReleased,
            RoutingStrategies.Tunnel, handledEventsToo: true);
        viewer.PointerCaptureLost += (_, _) =>
        {
            // 捕获被系统抢走 (窗口切换等) —— 复位, 避免悬挂的拖动态
            state.Active = false;
            state.Dragging = false;
        };

        void OnPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(viewer).Properties.IsLeftButtonPressed) return;
            state.Start = e.GetPosition(viewer);
            state.Origin = viewer.Offset;
            state.Dragging = false;
            state.Active = true;
        }

        void OnMoved(object? sender, PointerEventArgs e)
        {
            if (!state.Active) return;
            var p = e.GetPosition(viewer);
            var dx = p.X - state.Start.X;
            var dy = p.Y - state.Start.Y;
            if (!state.Dragging)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < state.Threshold) return;
                state.Dragging = true;
                // 越阈瞬间把捕获抢到 viewer: 按钮 LostCapture → 按压态取消 → 不触发 Click;
                // 后续移动/释放由 capture 直达 viewer (与隧道/气泡路由无关)
                e.Pointer.Capture(viewer);
            }
            // 手指往上拖 → 内容往下走，取负
            viewer.Offset = new Vector(
                state.Origin.X - dx,
                state.Origin.Y - dy);
            e.Handled = true;
        }

        void OnReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!state.Active) return;
            if (state.Dragging) e.Handled = true; // 拖动结束的释放不落回内层控件
            state.Active = false;
            state.Dragging = false;
        }
    }

    private sealed class State
    {
        public ScrollViewer Viewer = null!;
        public double Threshold;
        public Point Start;
        public Vector Origin;
        public bool Dragging;
        public bool Active;
    }
}
