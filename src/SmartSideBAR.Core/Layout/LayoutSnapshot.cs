// 窗口布局快照模型 (方案 §8.1, W9-10): 序列化 + 三级匹配打分 (exe → title 前缀 → 屏幕)。
// 定位不靠 hwnd (重启后失效), 按 (exeName, title 前缀, 显示器) 打分恢复。
namespace SmartSideBAR.Core.Layout;

public sealed record WindowLayoutEntry(
    string ExeName,
    string Title,
    int X, int Y, int W, int H,
    string State,          // normal | maximized | minimized
    int MonitorIndex);

public sealed record WindowLayoutProfile(string Name, List<WindowLayoutEntry> Entries);

public static class WindowLayoutMatcher
{
    /// <summary>
    /// 给候选窗口打恢复分: exe 全等 4 分 + title 前缀 3 分 + 显示器序 1 分。
    /// 分数 &lt; 4 视为不可恢复 (至少要 exe 对上)。
    /// </summary>
    public static int Score(WindowLayoutEntry saved, string candidateExe, string candidateTitle, int candidateMonitor)
    {
        var score = 0;
        if (string.Equals(saved.ExeName, candidateExe, StringComparison.OrdinalIgnoreCase)) score += 4;
        else return 0;
        if (!string.IsNullOrEmpty(saved.Title) &&
            candidateTitle.StartsWith(saved.Title, StringComparison.OrdinalIgnoreCase)) score += 3;
        if (saved.MonitorIndex == candidateMonitor) score += 1;
        return score;
    }
}
