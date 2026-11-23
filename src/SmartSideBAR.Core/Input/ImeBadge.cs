// 输入法角标文案 (2026-11 审计: 非中文布局此前直接输出 lang&0x3FF 的十六进制,
// 装日文/韩文键盘的老师会看到 "11" 这类无意义角标)。
// 纯函数放 Core: 不依赖 Win32, 可单测。
using System.Globalization;

namespace SmartSideBAR.Core.Input;

public static class ImeBadge
{
    /// <summary>
    /// Win32 键盘布局低 16 位 (HKL &amp; 0xFFFF) → 角标文案。
    /// 中文按子语言给 zh-CN/zh-TW/zh-SG/zh-HK; 其他语言给三字母 ISO 码 (JPN/KOR/ENG...);
    /// 系统不认识的布局回退 0x 十六进制, 不再输出裸十进制片段。
    /// </summary>
    public static string Format(ushort languageId)
    {
        if ((languageId & 0x3FF) == 0x04)
        {
            // MAKELANGID 子语言位: 1=繁体(TW) 2=简体(CN) 3=香港 4=新加坡 5=澳门。
            // (2026-11 修正: 旧代码把 1/2 映射反了, zh-CN 被显示成 zh-TW)
            return $"zh-{(languageId >> 10) switch
            {
                1 => "TW",
                2 => "CN",
                3 => "HK",
                4 => "SG",
                5 => "MO",
                _ => "CN",
            }}";
        }

        try
        {
            var culture = CultureInfo.GetCultureInfo(languageId);
            var iso = culture.ThreeLetterISOLanguageName;
            if (iso.Length == 3 && !string.Equals(iso, "ivl", StringComparison.OrdinalIgnoreCase))
            {
                return iso.ToUpperInvariant();
            }
        }
        catch (CultureNotFoundException)
        {
            // 自定义/未知布局: 走下面的十六进制兜底
        }
        return $"0x{languageId:X4}";
    }
}
