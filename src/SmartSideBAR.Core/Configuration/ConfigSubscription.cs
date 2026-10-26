// 配置键级订阅与锁定键。
// 借鉴 Class-Widgets-2 的 ConfigBaseModel: 变更回调按路径下发(_on_change / _config_path),
// 以及 _locked_keys 语义 —— 被策略层下发的键不允许用户层改写。
namespace SmartSideBAR.Core.Configuration;

/// <summary>
/// 按键路径的变更订阅。支持精确键与前缀(如 "display.")两种订阅粒度。
/// </summary>
public sealed class ConfigSubscription
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<Action>> _exact = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Prefix, Action Handler)> _prefix = new();
    private readonly HashSet<string> _lockedKeys = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>订阅某个精确键。</summary>
    public void Subscribe(string dotPath, Action handler)
    {
        lock (_gate)
        {
            if (!_exact.TryGetValue(dotPath, out var list))
                _exact[dotPath] = list = new List<Action>();
            list.Add(handler);
        }
    }

    /// <summary>订阅某个前缀(如 "display."), 该前缀下任意键变更都会触发。</summary>
    public void SubscribePrefix(string prefix, Action handler)
    {
        lock (_gate) _prefix.Add((prefix, handler));
    }

    public bool Unsubscribe(string dotPath, Action handler)
    {
        lock (_gate)
        {
            if (!_exact.TryGetValue(dotPath, out var list)) return false;
            var removed = list.Remove(handler);
            if (list.Count == 0) _exact.Remove(dotPath);
            return removed;
        }
    }

    /// <summary>标记锁定键(策略层下发, 用户层不可改)。</summary>
    public void Lock(string dotPath)
    {
        lock (_gate) _lockedKeys.Add(dotPath);
    }

    public void Unlock(string dotPath)
    {
        lock (_gate) _lockedKeys.Remove(dotPath);
    }

    public bool IsLocked(string dotPath)
    {
        lock (_gate)
        {
            if (_lockedKeys.Contains(dotPath)) return true;
            // 前缀锁定: "policy." 下全部视为锁定
            return _lockedKeys.Any(k => k.EndsWith(".", StringComparison.Ordinal)
                && dotPath.StartsWith(k, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>键变更后广播, 返回被触发的处理器数量。</summary>
    public int Notify(string dotPath)
    {
        List<Action>? exact = null;
        List<Action> prefix = new();
        lock (_gate)
        {
            if (_exact.TryGetValue(dotPath, out var list)) exact = list.ToList();
            foreach (var (p, h) in _prefix)
                if (dotPath.StartsWith(p, StringComparison.OrdinalIgnoreCase)) prefix.Add(h);
        }

        var fired = 0;
        if (exact is not null)
            foreach (var h in exact) { SafeInvoke(h); fired++; }
        foreach (var h in prefix) { SafeInvoke(h); fired++; }
        return fired;
    }

    private static void SafeInvoke(Action handler)
    {
        try { handler(); }
        catch { /* 订阅方异常不影响其它订阅方 */ }
    }
}
