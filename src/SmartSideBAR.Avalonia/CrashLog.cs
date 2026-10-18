// 崩溃取证 —— 常驻程序最怕"静默退出"：任务栏里图标没了，却查不到任何原因。
// 这里把三类未处理异常统一落盘到 logs/crash.log，不依赖日志系统已完成初始化。
using System.Text;

namespace SmartSideBAR.Avalonia;

internal static class CrashLog
{
    private static readonly object Gate = new();

    public static string Path { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SmartSideBAR", "logs", "crash.log");

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
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] source={source} fatal={fatal}");
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
    }
}
