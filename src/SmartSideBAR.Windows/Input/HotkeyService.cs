// Windows/Input/HotkeyService.cs —— §6.13 RegisterHotKey: 冲突检测从启发式变为系统事实 (P2-2)。
// WM_HOTKEY 经 §5.3 同一 WndProcHook 基础设施路由 → bus.Publish(HotkeyPressed)。
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Input;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Windows.Input;

public sealed record HotkeyPressed(string Slot);
public sealed record HotkeySlotInfo(string Slot, HotkeyCombo? Combo, bool Conflicted, IReadOnlyList<HotkeyCombo> Suggestions);

public interface IHotkeyService
{
    /// <summary>注册槽位; 冲突返回 false 并给出系统级冲突事实下的替代建议。</summary>
    bool Register(string slot, string hotkeyText);

    void Unregister(string slot);
    IReadOnlyList<HotkeySlotInfo> GetSlots();
}

public sealed class HotkeyService : IHotkeyService, IDisposable
{
    private static int _nextId = 1;

    private readonly IWndProcHook _hook;
    private readonly IEventBus _bus;
    private readonly ILogger<HotkeyService>? _log;
    private readonly object _gate = new();
    private readonly Dictionary<string, (int Id, HotkeyCombo Combo)> _active = [];
    private readonly Dictionary<string, IReadOnlyList<HotkeyCombo>> _suggestions = [];
    private nint _hwnd;
    private WndProcCallback? _callback;

    public HotkeyService(IWndProcHook hook, IEventBus bus, ILogger<HotkeyService>? log = null)
    {
        _hook = hook;
        _bus = bus;
        _log = log;
    }

    /// <summary>宿主窗口就绪后调用 (所有热键挂在同一消息窗口上)。</summary>
    public void Attach(nint hwnd)
    {
        lock (_gate)
        {
            if (_hwnd != 0) return;
            _hwnd = hwnd;
            _callback = OnWndProc;
            _hook.Add(hwnd, _callback);
        }
    }

    public bool Register(string slot, string hotkeyText)
    {
        if (!HotkeyParser.TryParse(hotkeyText, out var combo))
        {
            _log?.LogWarning("[Hotkey] 无法解析热键文本: {Slot}={Text}", slot, hotkeyText);
            return false;
        }
        lock (_gate)
        {
            if (_hwnd == 0) { _log?.LogWarning("[Hotkey] Attach 未调用, 忽略注册 {Slot}", slot); return false; }
            Unregister(slot);
            var id = _nextId++;
            if (!Win32Input.RegisterHotKey(_hwnd, id, combo.Modifiers, combo.VirtualKey))
            {
                var suggestions = HotkeyParser.SuggestAlternatives(combo);
                _suggestions[slot] = suggestions;
                _log?.LogWarning("[Hotkey] 注册冲突 (系统事实): {Slot}={Combo}, 建议 {Count} 个", slot, combo, suggestions.Count);
                return false;
            }
            _active[slot] = (id, combo);
            _suggestions.Remove(slot);
            _log?.LogInformation("[Hotkey] 已注册 {Slot}={Combo}", slot, combo);
            return true;
        }
    }

    public void Unregister(string slot)
    {
        lock (_gate)
        {
            if (_hwnd == 0 || !_active.Remove(slot, out var entry)) return;
            _ = Win32Input.UnregisterHotKey(_hwnd, entry.Id);
        }
    }

    public IReadOnlyList<HotkeySlotInfo> GetSlots()
    {
        lock (_gate)
        {
            var slots = _active.Keys.Union(_suggestions.Keys);
            return slots.Select(s => _active.TryGetValue(s, out var e)
                ? new HotkeySlotInfo(s, e.Combo, false, [])
                : new HotkeySlotInfo(s, null, true, _suggestions[s])).ToArray();
        }
    }

    private nint OnWndProc(nint hwnd, uint msg, nuint wParam, nint lParam, ref bool handled)
    {
        if (msg == Win32Input.WM_HOTKEY)
        {
            handled = true;
            var id = (int)wParam;
            lock (_gate)
            {
                foreach (var (slot, entry) in _active)
                {
                    if (entry.Id != id) continue;
                    _bus.Publish(new HotkeyPressed(slot));
                    break;
                }
            }
            return 0;
        }
        return 0; // 不拦截其余消息
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_hwnd == 0) return;
            foreach (var slot in _active.Keys.ToArray()) Unregister(slot);
            if (_callback is not null) _hook.Remove(_hwnd, _callback);
            _hwnd = 0;
        }
    }
}
