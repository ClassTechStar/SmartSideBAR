// LongshotStitcherTests: B6 回归锁, 重叠语义 = 「新帧顶部 k 行 == 已拼图尾部末 k 行」
using SmartSideBAR.Core.Capture;

namespace SmartSideBAR.Core.Tests;

public sealed class LongshotStitcherTests
{
    [Fact]
    public void FindOverlap_FrameTopMatchesTailBottom()
    {
        long[] tail = [1, 2, 3, 4, 5, 6];
        long[] frame = [4, 5, 6, 7, 8];
        Assert.Equal(3, LongshotStitcher.FindOverlap(tail, frame, 5));
    }

    [Fact]
    public void FindOverlap_WholeFrameAlreadyPresent_ReturnsFrameLength()
    {
        long[] tail = [1, 2, 3, 4, 5];
        long[] frame = [3, 4, 5];
        Assert.Equal(3, LongshotStitcher.FindOverlap(tail, frame, 3));
    }

    [Fact]
    public void FindOverlap_NoOverlap_ReturnsZero()
    {
        long[] tail = [1, 2, 3];
        long[] frame = [9, 8, 7];
        Assert.Equal(0, LongshotStitcher.FindOverlap(tail, frame, 3));
    }

    [Fact]
    public void FindOverlap_RespectsMaxOverlap()
    {
        long[] tail = [1, 2, 9, 4];
        long[] frame = [9, 4, 5];
        // 真实重叠 2 行, 上限也是 2 → 命中
        Assert.Equal(2, LongshotStitcher.FindOverlap(tail, frame, 2));

        // 真实重叠 3 行但上限只有 2, 且不存在 2 行重叠 → 0 (不得越界猜重叠)
        long[] tail2 = [1, 2, 3, 4];
        long[] frame2 = [2, 3, 4, 5];
        Assert.Equal(0, LongshotStitcher.FindOverlap(tail2, frame2, 2));
    }

    [Fact]
    public void FindOverlap_EmptyInputs_ReturnsZero()
    {
        Assert.Equal(0, LongshotStitcher.FindOverlap([], [1, 2], 4));
        Assert.Equal(0, LongshotStitcher.FindOverlap([1, 2], [], 4));
        Assert.Equal(0, LongshotStitcher.FindOverlap([1, 2], [1, 2], 0));
    }
}
