// EventBusTests —— IPC 事件推送的结构性替代 (附录 A)
using SmartSideBAR.Core.Messaging;

namespace SmartSideBAR.Core.Tests;

public sealed class EventBusTests
{
    [Fact]
    public void Publish_ReachesSubscriber()
    {
        var bus = new EventBus();
        var received = new List<string>();
        using var sub = bus.Subscribe<string>(received.Add);
        bus.Publish("hello");
        Assert.Equal(["hello"], received);
    }

    [Fact]
    public void Dispose_StopsDelivery()
    {
        var bus = new EventBus();
        var received = new List<string>();
        var sub = bus.Subscribe<string>(received.Add);
        bus.Publish("a");
        sub.Dispose();
        bus.Publish("b");
        Assert.Equal(["a"], received);
    }

    [Fact]
    public void MultipleHandlers_AllReceive()
    {
        var bus = new EventBus();
        var a = new List<int>();
        var b = new List<int>();
        using var _ = bus.Subscribe<int>(a.Add);
        using var __ = bus.Subscribe<int>(b.Add);
        bus.Publish(7);
        Assert.Equal([7], a);
        Assert.Equal([7], b);
    }

    [Fact]
    public void TypesAreIsolated()
    {
        var bus = new EventBus();
        var ints = new List<int>();
        var strings = new List<string>();
        using var _ = bus.Subscribe<int>(ints.Add);
        using var __ = bus.Subscribe<string>(strings.Add);
        bus.Publish(1);
        Assert.Empty(strings);
    }

    [Fact]
    public void HandlerException_DoesNotBlockOthers()
    {
        var bus = new EventBus();
        var received = new List<int>();
        using var throwing = bus.Subscribe<int>(_ => throw new InvalidOperationException("boom"));
        using var normal = bus.Subscribe<int>(received.Add);
        bus.Publish(5);
        Assert.Equal([5], received);
    }
}
