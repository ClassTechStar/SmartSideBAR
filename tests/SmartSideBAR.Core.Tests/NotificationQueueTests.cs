// 通知优先级队列 (P0-2): 级别优先、同级别 FIFO、来源序号、容量淘汰。
using SmartSideBAR.Core.Messaging;

namespace SmartSideBAR.Core.Tests;

public class NotificationQueueTests
{
    [Fact]
    public void 高级别先出队()
    {
        var q = new NotificationQueue();
        q.Enqueue("usb", "U 盘已插入", NotificationLevel.Info);
        q.Enqueue("app", "操作失败", NotificationLevel.Error);

        var first = q.Dequeue();
        Assert.Equal(NotificationLevel.Error, first!.Level);
        Assert.Equal("操作失败", first.Message);
    }

    [Fact]
    public void 同级别按入队顺序先进先出()
    {
        var q = new NotificationQueue();
        q.Enqueue("usb", "第一条");
        q.Enqueue("usb", "第二条");

        Assert.Equal("第一条", q.Dequeue()!.Message);
        Assert.Equal("第二条", q.Dequeue()!.Message);
    }

    [Fact]
    public void 来源序号影响同级别次序()
    {
        var q = new NotificationQueue(src => src == "reminder" ? 0 : 5);
        q.Enqueue("usb", "U 盘");
        q.Enqueue("reminder", "该上课了");

        Assert.Equal("reminder", q.Dequeue()!.Source);
    }

    [Fact]
    public void 强制置顶插到最前()
    {
        var q = new NotificationQueue();
        q.Enqueue("app", "普通", NotificationLevel.Info);
        q.Enqueue("app", "紧急", NotificationLevel.Info, priorityOverride: true);

        Assert.Equal("紧急", q.Dequeue()!.Message);
    }

    [Fact]
    public void 超过容量时淘汰最低级别()
    {
        var q = new NotificationQueue(capacity: 2);
        q.Enqueue("usb", "低1");
        q.Enqueue("app", "高", NotificationLevel.Error);
        q.Enqueue("usb", "低2");

        Assert.Equal(2, q.Count);
        var top = q.Dequeue()!;
        Assert.Equal(NotificationLevel.Error, top.Level);
    }

    [Fact]
    public void 空队列出队返回空()
    {
        var q = new NotificationQueue();
        Assert.Null(q.Dequeue());
        Assert.Null(q.Peek());
    }
}
