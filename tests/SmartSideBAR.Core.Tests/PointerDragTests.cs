// 拖拽采样换算与合并 (P1-1)。
using SmartSideBAR.Core.FloatBall;

namespace SmartSideBAR.Core.Tests;

public class PointerDragTests
{
    [Fact]
    public void 位移按缩放系数换算一次()
    {
        var target = PointerDrag.Target(
            new DragSample(100, 100),
            new DragSample(500, 500),
            new DragSample(510, 520),
            1.5);
        // 100 + 10*1.5 = 115; 100 + 20*1.5 = 130
        Assert.Equal(115, target.X);
        Assert.Equal(130, target.Y);
    }

    [Fact]
    public void 缩放为默认1时位移等长()
    {
        var target = PointerDrag.Target(
            new DragSample(0, 0), new DragSample(10, 10), new DragSample(20, 30), 1.0);
        Assert.Equal(10, target.X);
        Assert.Equal(20, target.Y);
    }

    [Fact]
    public void 无效缩放按1处理()
    {
        var target = PointerDrag.Target(
            new DragSample(0, 0), new DragSample(0, 0), new DragSample(10, 10), 0);
        Assert.Equal(10, target.X);
    }

    [Fact]
    public void 阈值判定取两轴最大位移()
    {
        var start = new DragSample(0, 0);
        Assert.False(PointerDrag.Exceeds(start, new DragSample(3, 4), 5));
        Assert.True(PointerDrag.Exceeds(start, new DragSample(6, 0), 5));
        Assert.True(PointerDrag.Exceeds(start, new DragSample(0, -6), 5));
    }

    [Fact]
    public async Task 合并队列只执行最新样本()
    {
        var executed = new List<int>();
        var gate = new TaskCompletionSource();
        var queue = new LatestMoveQueue<int>(async v =>
        {
            executed.Add(v);
            await gate.Task; // 故意卡住第一个, 让后续样本堆积
        });

        queue.Enqueue(1);
        queue.Enqueue(2);
        queue.Enqueue(3);
        // 等第一个进入处理
        await Task.Delay(50);
        gate.SetResult();
        await Task.Delay(100);

        // 1 被执行; 2 被 3 覆盖, 最终执行的是 3
        Assert.Contains(1, executed);
        Assert.Contains(3, executed);
        Assert.DoesNotContain(2, executed);
    }
}
