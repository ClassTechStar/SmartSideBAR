// Core/Input/HotkeyParser.cs —— "Ctrl+Shift+A" 文本 ↔ Win32 修饰键+虚拟键 (方案 §6.13)。
// 解析/替代建议为纯函数可单测; 冲突事实由 RegisterHotKey 返回值给出 (系统级, 非启发式)。
namespace SmartSideBAR.Core.Input;

public readonly record struct HotkeyCombo(uint Modifiers, uint VirtualKey)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & ModAlt) != 0) parts.Add("Alt");
        if ((Modifiers & ModCtrl) != 0) parts.Add("Ctrl");
        if ((Modifiers & ModShift) != 0) parts.Add("Shift");
        if ((Modifiers & ModWin) != 0) parts.Add("Win");
        parts.Add(VirtualKey switch
        {
            >= 0x30 and <= 0x39 => ((char)VirtualKey).ToString(),
            >= 0x41 and <= 0x5A => ((char)VirtualKey).ToString(),
            >= 0x70 and <= 0x7B => $"F{VirtualKey - 0x6F}",
            0x20 => "Space",
            0x2D => "Insert",
            0x2E => "Delete",
            0x21 => "PageUp",
            0x22 => "PageDown",
            _ => $"0x{VirtualKey:X2}",
        });
        return string.Join("+", parts);
    }

    public const uint ModAlt = 0x0001;
    public const uint ModCtrl = 0x0002;
    public const uint ModShift = 0x0004;
    public const uint ModWin = 0x0008;
}

public static class HotkeyParser
{
    private static readonly Dictionary<string, uint> ModMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ctrl"] = HotkeyCombo.ModCtrl,
        ["control"] = HotkeyCombo.ModCtrl,
        ["shift"] = HotkeyCombo.ModShift,
        ["alt"] = HotkeyCombo.ModAlt,
        ["win"] = HotkeyCombo.ModWin,
        ["super"] = HotkeyCombo.ModWin,
    };

    public static bool TryParse(string? text, out HotkeyCombo combo)
    {
        combo = default;
        if (string.IsNullOrWhiteSpace(text)) return false;
        uint mods = 0;
        uint? vk = null;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (ModMap.TryGetValue(raw, out var m))
            {
                mods |= m;
            }
            else if (raw.Length == 1 && char.IsAsciiLetterOrDigit(raw[0]))
            {
                var c = char.ToUpperInvariant(raw[0]);
                vk = c switch
                {
                    >= 'A' and <= 'Z' => (uint)c,
                    >= '0' and <= '9' => (uint)c,
                    _ => null,
                };
            }
            else if (raw.Length >= 2 && raw[0] is 'F' or 'f' && int.TryParse(raw[1..], out var f) && f is >= 1 and <= 12)
            {
                vk = (uint)(0x70 + f - 1);
            }
            else if (raw.Equals("Space", StringComparison.OrdinalIgnoreCase)) vk = 0x20;
            else if (raw.Equals("Insert", StringComparison.OrdinalIgnoreCase)) vk = 0x2D;
            else if (raw.Equals("Delete", StringComparison.OrdinalIgnoreCase)) vk = 0x2E;
            else return false;
        }
        if (vk is null) return false;
        combo = new HotkeyCombo(mods, vk.Value);
        return true;
    }

    /// <summary>冲突替代建议 (P2-2): 依序尝试换 Win→Ctrl、加/去 Shift、F 键邻位;
    /// 仅生成候选, 是否真冲突由系统 RegisterHotKey 判定。</summary>
    public static IReadOnlyList<HotkeyCombo> SuggestAlternatives(HotkeyCombo original)
    {
        var list = new List<HotkeyCombo>();
        void Add(uint mods, uint vk)
        {
            var c = new HotkeyCombo(mods, vk);
            if (!list.Contains(c) && c != original) list.Add(c);
        }
        var (m, vk) = (original.Modifiers, original.VirtualKey);
        if ((m & HotkeyCombo.ModWin) != 0) Add((m & ~HotkeyCombo.ModWin) | HotkeyCombo.ModCtrl, vk);
        Add(m ^ HotkeyCombo.ModShift, vk);                 // 加/去 Shift
        if ((m & HotkeyCombo.ModCtrl) != 0) Add(m | HotkeyCombo.ModAlt, vk);
        if (vk is >= 0x70 and <= 0x7B) Add(m, vk + 1);     // F 邻位
        else if (char.IsAsciiLetter((char)vk)) Add(m, (uint)(((char)vk - 'A' + 1) % 26 + 'A'));
        Add(m | HotkeyCombo.ModWin, vk);
        return list;
    }
}
