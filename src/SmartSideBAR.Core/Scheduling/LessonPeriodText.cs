// 课表文本 ⇄ LessonPeriod 互转 —— 设置页用一行文本编辑课表,
// 格式: "8:00-8:45 语文; 8:55-9:40 数学" (名称可省略, 分号/换行分隔)。
// 解析失败的段跳过; 全部失败返回空表。比赛演示: 现场输入一节 2 分钟后开始的课即可。
namespace SmartSideBAR.Core.Scheduling;

public static class LessonPeriodText
{
    public static List<LessonPeriod> Parse(string? text)
    {
        var result = new List<LessonPeriod>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        foreach (var raw in text.Split([';', '；', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var seg = raw.Trim();
            var dash = seg.IndexOfAny(['-', '—', '–']);
            if (dash <= 0) continue;
            var start = ParseMinutes(seg[..dash].Trim());
            var rest = seg[(dash + 1)..].Trim();
            var space = rest.IndexOfAny([' ', '　']);
            var end = ParseMinutes((space > 0 ? rest[..space] : rest).Trim());
            if (start is null || end is null || end <= start) continue;
            var name = space > 0 ? rest[(space + 1)..].Trim() : null;
            if (string.IsNullOrEmpty(name)) name = null;
            result.Add(new LessonPeriod(start.Value, end.Value, name));
        }
        return result;
    }

    public static string Format(IReadOnlyList<LessonPeriod> periods)
    {
        if (periods is null || periods.Count == 0) return string.Empty;
        return string.Join("; ", periods.Select(p =>
            $"{MinutesToText(p.StartMinute)}-{MinutesToText(p.EndMinute)}{(string.IsNullOrEmpty(p.Name) ? "" : " " + p.Name)}"));
    }

    private static int? ParseMinutes(string s)
    {
        var parts = s.Split(':');
        if (parts.Length is not (1 or 2)) return null;
        // "8:30" → 8 小时 30 分; "510" → 510 分钟
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[0].Trim(), out var h) || !int.TryParse(parts[1].Trim(), out var m)) return null;
            if (h is < 0 or > 23 || m is < 0 or > 59) return null;
            return h * 60 + m;
        }
        if (!int.TryParse(parts[0].Trim(), out var total)) return null;
        return total is >= 0 and <= 24 * 60 ? total : null;
    }

    private static string MinutesToText(int minutes)
    {
        var h = minutes / 60;
        var m = minutes % 60;
        return $"{h:00}:{m:00}";
    }
}
