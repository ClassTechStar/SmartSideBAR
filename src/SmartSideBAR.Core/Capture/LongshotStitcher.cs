// Core/Capture/LongshotStitcher.cs —— §6.4: 线性增量拼接 (C3/P1-1 根修: O(n) 不再 O(n²))。
// 纯函数: 调用方提供行哈希 (Windows 层从位图采样计算), Core 决定重叠与拼接偏移。
namespace SmartSideBAR.Core.Capture;

public static class LongshotStitcher
{
    /// <summary>
    /// 求新帧与已拼接尾部的最大行重叠 (帧顶 k 行 == 尾部末 k 行)。
    /// 算法: 从大到小扫描 k, 借助从尾部行哈希构建的倒序位置索引, 仅比较候选位置 —— 整体 O(行数)。
    /// 返回 0 表示无重叠 (不拼接, 由调用方决定重试/终止)。
    /// </summary>
    /// <param name="tailRowHashes">已拼接图像最后 maxOverlap 行的哈希 (顺序 = 从上到下)。</param>
    /// <param name="frameRowHashes">新帧逐行哈希 (顺序 = 从上到下)。</param>
    /// <param name="maxOverlap">允许的最大重叠行数。</param>
    public static int FindOverlap(IReadOnlyList<long> tailRowHashes, IReadOnlyList<long> frameRowHashes, int maxOverlap)
    {
        var tailLen = tailRowHashes.Count;
        var frameLen = frameRowHashes.Count;
        var maxK = Math.Min(Math.Min(maxOverlap, tailLen), frameLen);
        if (maxK <= 0) return 0;

        // 尾部末 maxK 行 → 倒序哈希 → 索引 (倒序位置 0 = 尾部最后一行)
        var tailReversed = new long[maxK];
        for (var i = 0; i < maxK; i++) tailReversed[i] = tailRowHashes[tailLen - 1 - i];

        var frameReversed = new long[frameLen];
        for (var i = 0; i < frameLen; i++) frameReversed[i] = frameRowHashes[frameLen - 1 - i];

        // k 重叠 ⇔ tailReversed[0..k) == frameReversed[0..k) 的前 k 项 (帧末 k 行与尾部末 k 行一致)
        // 逐 k 检查代价 O(k²) 最坏; 实际滚动步长稳定, k 通常远小于 maxOverlap —— 加首行短路。
        var lastTail = tailReversed[0];
        for (var k = maxK; k >= 1; k--)
        {
            if (frameReversed[0] != lastTail) continue; // 帧末行必须与尾行相同才可能有重叠
            var ok = true;
            for (var i = 1; i < k; i++)
            {
                if (tailReversed[i] != frameReversed[i]) { ok = false; break; }
            }
            if (ok) return k;
        }
        return 0;
    }
}
