// 有界时间窗口历史（借鉴 SidebarDiagnostics GraphModel 的过期记录裁剪）。
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Core.Tests;

public class TimeWindowHistoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void 容量上限会丢最旧项()
    {
        var h = new TimeWindowHistory<int>(3);
        h.Add(1, T0);
        h.Add(2, T0.AddSeconds(1));
        h.Add(3, T0.AddSeconds(2));
        h.Add(4, T0.AddSeconds(3));
        Assert.Equal(new[] { 2, 3, 4 }, h.Snapshot());
        Assert.Equal(3, h.Count);
    }

    [Fact]
    public void 时间窗口外的记录被裁剪()
    {
        var h = new TimeWindowHistory<int>(50, TimeSpan.FromMinutes(10));
        h.Add(1, T0);
        h.Add(2, T0.AddMinutes(5));
        h.Add(3, T0.AddMinutes(20)); // 触发裁剪：1 和 2 都超过 10 分钟
        Assert.Equal(new[] { 3 }, h.Snapshot());
    }

    [Fact]
    public void 仅容量约束时不做时间裁剪()
    {
        var h = new TimeWindowHistory<int>(10);
        h.Add(1, T0);
        h.Add(2, T0.AddYears(1));
        Assert.Equal(2, h.Count);
        Assert.Equal(0, h.Prune(T0.AddYears(2)));
    }

    [Fact]
    public void 主动Prune回收过期项()
    {
        var h = new TimeWindowHistory<int>(50, TimeSpan.FromMinutes(10));
        h.Add(1, T0);
        h.Add(2, T0.AddMinutes(9));
        Assert.Equal(1, h.Prune(T0.AddMinutes(11)));
        Assert.Equal(new[] { 2 }, h.Snapshot());
    }

    [Fact]
    public void 最新在前快照顺序正确()
    {
        var h = new TimeWindowHistory<string>(5);
        h.Add("a", T0);
        h.Add("b", T0.AddSeconds(1));
        h.Add("c", T0.AddSeconds(2));
        Assert.Equal(new[] { "c", "b", "a" }, h.SnapshotNewestFirst());
    }

    [Fact]
    public void Clear_清空()
    {
        var h = new TimeWindowHistory<int>(5);
        h.Add(1, T0);
        h.Clear();
        Assert.Empty(h.Snapshot());
    }

    [Fact]
    public void 容量下限为1()
    {
        var h = new TimeWindowHistory<int>(0);
        h.Add(1, T0);
        h.Add(2, T0);
        Assert.Single(h.Snapshot());
    }
}
