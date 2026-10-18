// 日志轮转策略（P2-2）—— 纯逻辑，放 Core 便于单测。
// 背景：常驻教学一体机上 FileLoggerProvider 只按天分文件，长期不清理会无限增长。
// 这里给出两条约束：按天保留 N 份 + 单文件超过阈值即滚动。
namespace SmartSideBAR.Core.Diagnostics;

public static class LogRotation
{
    public const int DefaultRetentionDays = 14;
    public const long DefaultMaxBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>返回应删除的过期日志文件（按最后写入时间判定，不抛异常）。</summary>
    public static IReadOnlyList<string> FindExpired(string logDir, string searchPattern, int retentionDays, DateTime now)
    {
        var expired = new List<string>();
        if (retentionDays <= 0 || !Directory.Exists(logDir)) return expired;

        try
        {
            var cutoff = now.Date.AddDays(-retentionDays);
            foreach (var file in Directory.EnumerateFiles(logDir, searchPattern))
            {
                DateTime lastWrite;
                try { lastWrite = File.GetLastWriteTime(file); }
                catch { continue; }
                if (lastWrite.Date < cutoff) expired.Add(file);
            }
        }
        catch
        {
            // 目录不可读时不阻断
        }
        return expired;
    }

    /// <summary>删除过期文件；返回实际删除数量（单个失败不影响其余）。</summary>
    public static int Prune(string logDir, string searchPattern, int retentionDays, DateTime now)
    {
        var removed = 0;
        foreach (var file in FindExpired(logDir, searchPattern, retentionDays, now))
        {
            try
            {
                File.Delete(file);
                removed++;
            }
            catch
            {
                // 被占用（例如当天正在写）→ 跳过，下次再试
            }
        }
        return removed;
    }

    /// <summary>当前写入量是否应触发滚动。</summary>
    public static bool ShouldRoll(long currentBytes, long maxBytes)
    {
        if (maxBytes <= 0) return false;
        return currentBytes >= maxBytes;
    }

    /// <summary>滚动文件名：ssb-yyyyMMdd_HHmmss.log（同一天内多次滚动也不会重名冲突到秒级）。</summary>
    public static string RolledName(DateTime day, DateTime now) => $"ssb-{day:yyyyMMdd}_{now:HHmmss}.log";

    public static string DailyName(DateTime day) => $"ssb-{day:yyyyMMdd}.log";
}
