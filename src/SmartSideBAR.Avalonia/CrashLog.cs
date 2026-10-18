// 崩溃取证 —— 常驻程序最怕"静默退出"：任务栏里图标没了，却查不到任何原因。
// P2-3：除文本日志外，再落一份结构化 JSON（含版本/DPI/当前面板/OS 版本），便于聚合与关联。
// 这里把 AppDomain / TaskScheduler / UI 线程三路未处理异常统一收集。
using System.Text;
using System.Text.Json;
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Avalonia;

internal static class CrashLog
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SmartSideBAR", "logs", "crash.log");

    /// <summary>最近一次上下文（由 WindowManager 在面板切换/DPI 变化时刷新）。</summary>
    public static CrashContext? LastContext { get; set; }

    /// <summary>进程级取证。注意：此时不要碰 Dispatcher.UIThread ——
    /// 平台尚未初始化，提前访问会把 Dispatcher 绑到空实现，导致 MainLoop 抛
    /// PlatformNotSupportedException（本文件初版就踩了这个坑）。</summary>
    public static void InstallProcessWide()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write("AppDomain", e.ExceptionObject as Exception, fatal: e.IsTerminating);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("TaskScheduler", e.Exception, fatal: false);
            e.SetObserved();
        };
    }

    /// <summary>UI 线程取证。必须在 AppBuilder 初始化完成后调用。</summary>
    public static void InstallDispatcher()
    {
        global::Avalonia.Threading.Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Write("UIThread", e.Exception, fatal: false);
        };
    }

    public static void Write(string source, Exception? ex, bool fatal)
    {
        var context = LastContext ?? CrashContext.Unknown;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] source={source} fatal={fatal}");
            sb.AppendLine($"context: version={context.Version} dpi={context.DpiScale:0.##} panel={context.ActivePanel} os={context.OsBuild}");
            sb.AppendLine(ex?.ToString() ?? "(no exception object)");
            sb.AppendLine(new string('-', 80));
            lock (Gate)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                File.AppendAllText(Path, sb.ToString());
            }
        }
        catch
        {
            // 取证本身失败时绝不能再抛
        }

        WriteStructured(source, ex, fatal, context);
    }

    /// <summary>结构化副本：crash-{yyyyMMddHHmmss}-{source}.json，便于聚合/上报。</summary>
    private static void WriteStructured(string source, Exception? ex, bool fatal, CrashContext context)
    {
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["timestamp"] = DateTimeOffset.Now.ToString("o"),
                ["source"] = source,
                ["fatal"] = fatal,
                ["version"] = context.Version,
                ["dpiScale"] = context.DpiScale,
                ["activePanel"] = context.ActivePanel,
                ["osBuild"] = context.OsBuild,
                ["safeMode"] = SafeMode.FromEnvironment(),
                ["type"] = ex?.GetType().FullName,
                ["message"] = ex?.Message,
                ["stack"] = ex?.StackTrace,
                ["inner"] = ex?.InnerException?.ToString(),
            };

            var dir = System.IO.Path.GetDirectoryName(Path)!;
            Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(
                dir, $"crash-{DateTime.Now:yyyyMMddHHmmss}-{source}.json");
            File.WriteAllText(file, JsonSerializer.Serialize(payload, JsonOptions));
        }
        catch
        {
            // 同上：取证失败不能影响退出流程
        }
    }
}
