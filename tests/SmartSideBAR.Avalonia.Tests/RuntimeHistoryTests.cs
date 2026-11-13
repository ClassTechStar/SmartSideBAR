// 通知中心的有界历史 + 统一时钟暂停/恢复（借鉴 SidebarDiagnostics 的两处运行时模式）。
using Avalonia.Headless.XUnit;
using SmartSideBAR.Avalonia.Ui;

namespace SmartSideBAR.Avalonia.Tests;

public class RuntimeHistoryTests
{
    [Fact]
    public void 通知最新在前且容量上限生效()
    {
        var center = new NotificationCenter();
        for (var i = 0; i < 60; i++) center.Push($"msg-{i}", "info");
        Assert.Equal(50, center.Items.Count);
        Assert.Equal("msg-59", center.Items[0].Message);
        Assert.Equal("msg-10", center.Items[^1].Message);
    }

    [Fact]
    public void 新通知不会过期且Prune不误删()
    {
        var center = new NotificationCenter();
        center.Push("今天", "info");
        Assert.Equal(0, center.PruneExpired());
        Assert.Single(center.Items);
    }

    [Fact]
    public void 历史快照不是可写内部列表()
    {
        var center = new NotificationCenter();
        center.Push("a", "info");
        var snapshot = center.Items;
        center.Push("b", "info");
        Assert.Single(center.Items, x => x.Message == "a");
        Assert.Single(snapshot); // 旧快照不随后续 Push 变化
    }

    [Fact]
    public void 按Id删除与清空()
    {
        var center = new NotificationCenter();
        center.Push("a", "info");
        var id = center.Items[0].Id;
        center.Push("b", "warn");
        center.Remove(id);
        Assert.Single(center.Items);
        Assert.Equal("b", center.Items[0].Message);
        center.Clear();
        Assert.Empty(center.Items);
    }

    [Fact]
    public void 删除不存在的Id不触发变更()
    {
        var center = new NotificationCenter();
        var fired = 0;
        center.Changed += () => fired++;
        center.Remove("no-such-id");
        Assert.Equal(0, fired);
    }

    [AvaloniaFact]
    public void 统一时钟可暂停与恢复()
    {
        // 先确保宿主已初始化（可能由其他测试先创建）
        ClockHost.EnsureStarted();
        ClockHost.Pause();
        Assert.True(ClockHost.IsPaused);
        ClockHost.Resume();
        Assert.False(ClockHost.IsPaused);
    }
}
