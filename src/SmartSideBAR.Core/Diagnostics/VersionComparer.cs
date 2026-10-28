// 版本号比较与发布资产匹配 (P2-4 应用内更新)。
// 借鉴 CyreneCompass 的 updater.js（normalizeVersion / compareVersions / isInstaller 正则）。
namespace SmartSideBAR.Core.Diagnostics;

public static class VersionComparer
{
    /// <summary>去掉前缀 v / 空白后按点分段比较。返回 &lt;0 / 0 / &gt;0。</summary>
    public static int Compare(string? left, string? right)
    {
        var a = Parse(left);
        var b = Parse(right);
        var n = Math.Max(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            var av = i < a.Length ? a[i] : 0;
            var bv = i < b.Length ? b[i] : 0;
            if (av != bv) return av < bv ? -1 : 1;
        }
        return 0;
    }

    public static bool IsNewer(string? candidate, string? current) => Compare(candidate, current) > 0;

    /// <summary>把 "v1.5.0" 解析成数字段；非数字段按 0 处理。</summary>
    public static int[] Parse(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s[1..];
        if (s.Length == 0) return [0];
        var parts = s.Split('.');
        var result = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            // 允许 "1.5.0-beta" 这类后缀：只取前导数字
            var seg = parts[i];
            var digits = new string(seg.TakeWhile(char.IsDigit).ToArray());
            result[i] = int.TryParse(digits, out var v) ? v : 0;
        }
        return result;
    }

    /// <summary>
    /// 判断文件名是否为 Windows x64 安装包。
    /// 默认规则：以 SmartSideBAR 开头、含 x64、以 -Setup.exe / .exe 结尾。
    /// </summary>
    public static bool IsInstaller(string? name, string pattern = @"^SmartSideBAR[-_].*x64.*-Setup\.exe$")
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        try
        {
            return System.Text.RegularExpressions.Regex.IsMatch(
                name, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
