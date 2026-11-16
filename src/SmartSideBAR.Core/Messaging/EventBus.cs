// 替代 Electron IPC 事件推送 (附录 A): 同进程类型安全发布/订阅。
using Microsoft.Extensions.Logging;

namespace SmartSideBAR.Core.Messaging;

public interface IEventBus
{
    /// <summary>订阅某类型事件; Dispose 即退订。返回时快照已含本次订阅。</summary>
    IDisposable Subscribe<T>(Action<T> handler);

    /// <summary>发布事件到当前已注册的 T 处理器; 处理器异常不阻断其他处理器。</summary>
    void Publish<T>(T evt);
}

public sealed class EventBus(ILogger<EventBus>? log = null) : IEventBus
{
    private readonly object _gate = new();
    private Dictionary<Type, List<Delegate>> _handlers = [];

    public IDisposable Subscribe<T>(Action<T> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list))
            {
                list = [];
                _handlers[typeof(T)] = list;
            }
            list.Add(handler);
        }
        return new Unsubscription(this, typeof(T), handler);
    }

    public void Publish<T>(T evt)
    {
        Delegate[] snapshot;
        lock (_gate)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0) return;
            snapshot = [.. list];
        }
        foreach (var d in snapshot)
        {
            try { ((Action<T>)d)(evt); }
            catch (Exception ex)
            {
                // 单个处理器异常不得阻断事件链 (侧栏常驻场景);
                // P1-9: 记一条日志, 教学机上 handler 异常不再完全不可诊断
                log?.LogWarning(ex, "[EventBus] {Type} 的事件处理器抛出异常", typeof(T).Name);
            }
        }
    }

    private sealed class Unsubscription(EventBus bus, Type eventType, Delegate handler) : IDisposable
    {
        public void Dispose()
        {
            lock (bus._gate)
            {
                if (bus._handlers.TryGetValue(eventType, out var list))
                {
                    list.Remove(handler);
                    if (list.Count == 0) bus._handlers.Remove(eventType);
                }
            }
        }
    }
}
