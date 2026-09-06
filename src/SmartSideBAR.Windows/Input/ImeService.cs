// Windows/Input/ImeService.cs —— §6.1 (ADR-M6): Win32 键盘布局直调, 零 PowerShell 子进程。
// v1.2.0 常驻 PS 守护 (<50ms) → 消息直发 (<5ms); config.ime.slot1/slot2 首次被消费 (审计 #1 遗留)。
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Windows.Input;

public sealed record ImeState(string Locale, bool IsChinese);

public sealed record ImeChanged(string Locale, bool IsChinese);

public interface IImeService
{
    ImeState GetState();
    bool Toggle();
    bool SelectChinese();
    bool SelectEnglish();
}

public sealed class ImeService(IEventBus bus, ILogger<ImeService>? log = null) : IImeService
{
    private const ushort LangZh = 0x0004;
    private const ushort LangEn = 0x0009;

    public ImeState GetState()
    {
        var fg = Win32Input.GetForegroundWindow();
        var thread = Win32Input.GetWindowThreadProcessId(fg, out _);
        var hkl = Win32Input.GetKeyboardLayout(thread);
        return FromHkl(hkl);
    }

    public bool Toggle()
    {
        var current = GetState();
        return current.IsChinese ? SelectEnglish() : SelectChinese();
    }

    public bool SelectChinese() => SelectByLanguage(LangZh);
    public bool SelectEnglish() => SelectByLanguage(LangEn);

    private static ImeState FromHkl(nint hkl)
    {
        var lang = (ushort)(hkl & 0xFFFF);
        var isZh = (lang & 0x3FF) == LangZh;
        var locale = isZh
            ? $"zh-{(lang >> 10) switch { 1 => "CN", 2 => "TW", 4 => "SG", 5 => "HK", _ => "CN" }}"
            : $"{lang & 0x3FF:x2}";
        return new ImeState(locale, isZh);
    }

    private bool SelectByLanguage(ushort primaryLang)
    {
        var layouts = Win32Input.GetKeyboardLayouts();
        if (layouts.Length == 0)
        {
            log?.LogWarning("[IME] 未枚举到键盘布局");
            return false;
        }
        nint target = 0;
        foreach (var hkl in layouts)
        {
            if (((ushort)(hkl & 0xFFFF) & 0x3FF) == primaryLang) { target = hkl; break; }
        }
        if (target == 0)
        {
            // 无目标语言布局: 回退到与当前不同的第一个布局 (轮换语义, fallbackSwap)
            var current = Win32Input.GetKeyboardLayout(Win32Input.GetWindowThreadProcessId(Win32Input.GetForegroundWindow(), out _));
            target = layouts.FirstOrDefault(h => h != current, layouts[0]);
        }

        // 激活目标布局并向前台窗口广播切换请求 (KLID = 8 位十六进制: 高 4 位设备, 低 4 位语言)
        var klid = $"{(uint)target >> 16:X4}{(ushort)target:X4}";
        _ = Win32Input.LoadKeyboardLayout(klid, Win32Input.KLF_ACTIVATE);
        var fg = Win32Input.GetForegroundWindow();
        var ok = fg != 0 && Win32Input.PostMessage(fg, Win32Input.WM_INPUTLANGCHANGEREQUEST, 0, target);
        if (ok)
        {
            var state = FromHkl(target);
            bus.Publish(new ImeChanged(state.Locale, state.IsChinese));
            log?.LogInformation("[IME] 切换 → {Locale}", state.Locale);
        }
        return ok;
    }
}
