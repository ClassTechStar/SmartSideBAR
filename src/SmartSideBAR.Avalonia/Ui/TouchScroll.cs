// 触屏拖动滚动 (P2-2)。
// 借鉴 DiffiTouch 的 TouchListView：列表按像素滚动、超过阈值才判定为拖动，
// 手指微动不会被当成点击，也不至于一碰就滚。Avalonia 自带手势，这里补的是
// 「拖动即滚 + 阈值」这一层，便于在触屏一体机上调到顺手。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace SmartSideBAR.Avalonia.Ui;

public static class TouchScroll
{
    /// <summary>默认拖动判定阈值（DIP）。</summary>
    public const double DefaultThreshold = 10;

    /// <summary>为 ScrollViewer 接入「按住拖动即滚动」。</summary>
    public static void Attach(ScrollViewer viewer, double threshold = DefaultThreshold)
    {
        var state = new State { Viewer = viewer, Threshold = threshold };

        viewer.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(viewer).Properties.IsLeftButtonPressed) return;
            var p = e.GetPosition(viewer);
            state.Start = p;
            state.Origin = viewer.Offset;
            state.Dragging = false;
            state.Active = true;
        };

        viewer.PointerMoved += (_, e) =>
        {
            if (!state.Active) return;
            var p = e.GetPosition(viewer);
            var dx = p.X - state.Start.X;
            var dy = p.Y - state.Start.Y;
            if (!state.Dragging)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) < state.Threshold) return;
                state.Dragging = true;
            }
            // 手指往上拖 → 内容往下走，取负
            viewer.Offset = new Vector(
                state.Origin.X - dx,
                state.Origin.Y - dy);
            e.Handled = true;
        };

        viewer.PointerReleased += (_, e) =>
        {
            state.Active = false;
            state.Dragging = false;
        };
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
