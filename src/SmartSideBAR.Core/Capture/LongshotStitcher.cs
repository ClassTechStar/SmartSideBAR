// Core/Capture/LongshotStitcher.cs —— §6.4: 线性增量拼接 (C3/P1-1 根修: O(n) 不再 O(n²))。
// 纯函数: 调用方提供行哈希 (Windows 层从位图采样计算), Core 决定重叠与拼接偏移。
namespace SmartSideBAR.Core.Capture;

public static class LongshotStitcher
{
    /// <summary>
    /// 求新帧与已拼接尾部的最大行重叠: 新帧**顶部 k 行** == 已拼图**尾部末 k 行**。
    /// 返回 0 表示无重叠 (不拼接, 由调用方决定重试/终止)。
    /// B6 根修: 原实现把「尾图末 k 行」与「新帧末 k 行」相比 (两侧都取末行),
    /// 与文档语义及调用方传参 (尾图) 全部错位, 导致重叠几乎恒为 0 → 长图重复条带。
    /// </summary>
    /// <param name="tailRowHashes">已拼接图像最后 maxOverlap 行的哈希 (顺序 = 从上到下)。</param>
    /// <param name="frameRowHashes">新帧逐行哈希 (顺序 = 从上到下)。</param>
    /// <param name="maxOverlap">允许的最大重叠行数。</param>
    public static int FindOverlap(IReadOnlyList<long> tailRowHashes, IReadOnlyList<long> frameRowHashes, int maxOverlap)
    {
        ArgumentNullException.ThrowIfNull(tailRowHashes);
        ArgumentNullException.ThrowIfNull(frameRowHashes);
        var tailLen = tailRowHashes.Count;
        var frameLen = frameRowHashes.Count;
        var maxK = Math.Min(Math.Min(maxOverlap, tailLen), frameLen);
        if (maxK <= 0) return 0;

        var frameFirst = frameRowHashes[0];
        for (var k = maxK; k >= 1; k--)
        {
            var tailStart = tailLen - k;
            // 首行不等即可判定该 k 不成立 (绝大多数候选在此被剪掉)
            if (tailRowHashes[tailStart] != frameFirst) continue;
            var ok = true;
            for (var i = 1; i < k; i++)
            {
                if (tailRowHashes[tailStart + i] != frameRowHashes[i]) { ok = false; break; }
            }
            if (ok) return k;
        }
        return 0;
    }
}
