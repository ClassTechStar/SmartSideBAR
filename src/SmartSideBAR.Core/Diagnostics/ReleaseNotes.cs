// 数据驱动的版本变更说明。
// 借鉴 SidebarDiagnostics 的 ChangeLog.json + ChangeLogModel（启动时比对版本展示更新内容）。
// 适配：本项目不额外维护一份 JSON，而是直接解析随程序集嵌入的 CHANGELOG.md，
// 保证人工文档与运行时展示同源；且只在设置窗「关于」页展示，不自动弹窗打断课堂。
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SmartSideBAR.Core.Diagnostics;

public sealed record ReleaseNotesEntry(string Version, string? Date, IReadOnlyList<string> Changes);

public static partial class ReleaseNotes
{
    private const string ResourceName = "CHANGELOG.md";

    /// <summary>解析 Keep a Changelog 风格的 markdown：## [版本] - 日期 + 下面的 - 条目。</summary>
    public static IReadOnlyList<ReleaseNotesEntry> Parse(string markdown)
    {
        var result = new List<ReleaseNotesEntry>();
        if (string.IsNullOrWhiteSpace(markdown)) return result;

        ReleaseNotesEntry? current = null;
        var changes = new List<string>();

        void Flush()
        {
            if (current is not null)
                result.Add(current with { Changes = changes.ToArray() });
            changes.Clear();
        }

        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            var heading = HeadingRegex().Match(line);
            if (heading.Success)
            {
                Flush();
                current = new ReleaseNotesEntry(
                    heading.Groups["v"].Value,
                    heading.Groups["d"].Success ? heading.Groups["d"].Value.Trim() : null,
                    []);
                continue;
            }
            if (current is not null && line.StartsWith("- ", StringComparison.Ordinal))
                changes.Add(line[2..].Trim());
        }
        Flush();
        return result;
    }

    /// <summary>按版本号查找（忽略大小写、忽略构建元数据）。</summary>
    public static ReleaseNotesEntry? Find(IEnumerable<ReleaseNotesEntry> entries, string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return null;
        var wanted = Normalize(version);
        return entries.FirstOrDefault(e =>
            string.Equals(Normalize(e.Version), wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>从程序集嵌入的 CHANGELOG.md 读取指定版本的条目；任何失败返回 null。</summary>
    public static ReleaseNotesEntry? ForVersion(string? version)
    {
        try
        {
            using var stream = typeof(ReleaseNotes).Assembly.GetManifestResourceStream(ResourceName);
            if (stream is null) return null;
            using var reader = new StreamReader(stream);
            return Find(Parse(reader.ReadToEnd()), version);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>当前程序集版本（三段）。</summary>
    public static string CurrentVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version;
            return v is null ? "0.0.0" : $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
        }
    }

    /// <summary>把 "v1.6.0+abc" / "1.6.0.0" 归一到 "1.6.0"。</summary>
    public static string Normalize(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return "";
        var s = version.Trim().TrimStart('v', 'V');
        var plus = s.IndexOf('+');
        if (plus >= 0) s = s[..plus];
        var parts = s.Split('.');
        if (parts.Length < 3) return s;
        return string.Join('.', parts.Take(3).Select(p =>
            int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n.ToString(CultureInfo.InvariantCulture)
                : p));
    }

    [GeneratedRegex(@"^##\s*\[(?<v>[^\]]+)\]\s*(?:-\s*(?<d>.+))?$", RegexOptions.Compiled)]
    private static partial Regex HeadingRegex();
}
