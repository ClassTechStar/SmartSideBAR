// MVVM 基座 (M0): State/VM 的生命周期订阅。
// 统一把 IEventBus 订阅收进一个可释放列表, 避免每个 VM 各写一份退订样板;
// 将来 ConfigService 的变更也通过这里进入 State 层 (单一状态源)。
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;

namespace SmartSideBAR.Avalonia.ViewModels.Infrastructure;

public abstract class StateBase : ObservableObject, IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];
    private bool _disposed;

    /// <summary>登记一个需要随 State 一起释放的订阅。</summary>
    protected void Track(IDisposable subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        if (_disposed)
        {
            subscription.Dispose();
            return;
        }
        _subscriptions.Add(subscription);
    }

    /// <summary>订阅配置变更 (ConfigService 每次落盘都会广播)。</summary>
    protected void SubscribeConfig(IEventBus bus, Action<ConfigChanged> handler) =>
        Track(bus.Subscribe(handler));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var subscription in _subscriptions) subscription.Dispose();
        _subscriptions.Clear();
        GC.SuppressFinalize(this);
    }
}
