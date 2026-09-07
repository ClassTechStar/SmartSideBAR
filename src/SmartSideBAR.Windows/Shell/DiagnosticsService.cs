// Windows/Shell/DiagnosticsService.cs —— §6.12: 7 项真实探测聚合 + 诊断包导出 (P1-12 语义)。
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Recording;


namespace SmartSideBAR.Windows.Shell;

public interface IDiagnosticsService
{
    IReadOnlyDictionary<string, string> RunFullCheck();
    string ExportPack(string dir);
}

public sealed partial class DiagnosticsService(
    ConfigService config,
    AppBarService appBar,
    IImeService ime,
    IUsbWatcher usb,
    IPrinterMonitor printer,
    IRecorderApi recorder,
    IShellService shell,
    ILogger<DiagnosticsService>? log = null) : IDiagnosticsService
{
    public IReadOnlyDictionary<string, string> RunFullCheck()
    {
        var result = new Dictionary<string, string>();
        try
        {
            result["os"] = Environment.OSVersion.VersionString;
            result["process"] = Environment.Is64BitProcess ? "x64" : "x86";
            result["screen"] = ScreenProbe();
            result["touch"] = TouchProbe();
            var imeState = ime.GetState();
            result["ime"] = $"{imeState.Locale} (zh={imeState.IsChinese})";
            var usbList = usb.ListRemovable();
            result["usb"] = usbList.Count == 0 ? "无可移动盘" : string.Join(", ", usbList.Select(d => $"{d.Drive} {d.SizeGb}GB"));
            var printers = printer.Query();
            result["printers"] = printers.Count == 0 ? "无打印机" : string.Join(", ", printers.Select(p => $"{p.Name}={p.WireState}"));
            result["appbar"] = appBar.IsRegistered ? $"已注册 ({appBar.GrantedRect})" : "未注册 (Topmost 回退或未启动)";
            result["recorder"] = recorder.IsSupported() ? "可用 (WGC)" : "不可用 (需 Win10 1803+)";
            result["autolaunch"] = shell.GetAutoLaunch() ? "已启用" : "未启用";
            result["captureDir"] = ProbeWritable(ExpandDir(config.Current.Capture.Dir));
            result["recorderDir"] = ProbeWritable(ExpandDir(config.Current.Recorder.Dir));
            result["configPath"] = config.Current.Version.ToString();
        }
        catch (Exception ex)
        {
            result["error"] = ex.Message;
            log?.LogWarning(ex, "[Diag] 巡检部分失败");
        }
        return result;
    }

    public string ExportPack(string dir)
    {
        Directory.CreateDirectory(dir);
        var report = new Dictionary<string, object>
        {
            ["generatedAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["checks"] = RunFullCheck(),
            ["config"] = config.Current,
        };
        var json = JsonSerializer.Serialize(report, ConfigJsonContext.Default.Options);
        var reportPath = Path.Combine(dir, $"diag_{DateTime.Now:yyyyMMdd_HHmmss}");
        Directory.CreateDirectory(reportPath);
        File.WriteAllText(Path.Combine(reportPath, "report.json"), json, Encoding.UTF8);

        // 日志打包 (7d 滚动目录整体复制)
        var logsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SmartSideBAR", "logs");
        if (Directory.Exists(logsDir))
        {
            var zipPath = Path.Combine(reportPath, "logs.zip");
            ZipFile.CreateFromDirectory(logsDir, zipPath);
        }
        log?.LogInformation("[Diag] 诊断包已导出 {Path}", reportPath);
        return reportPath;
    }

    private static string ExpandDir(string p) => p
        .Replace("{Pictures}", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures))
        .Replace("{Videos}", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos))
        .Replace("{Home}", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    private static string ProbeWritable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".probe-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return $"{dir} ✓ 可写";
        }
        catch (Exception ex)
        {
            return $"{dir} ✗ {ex.Message}";
        }
    }

    private static string ScreenProbe()
    {
        using var scope = new AppBarApi.PhysicalDpiScope();
        var hMonitor = AppBarNative.MonitorFromPoint(new AppBarNative.POINT(0, 0), 1);
        var mi = new AppBarNative.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<AppBarNative.MONITORINFO>() };
        if (!AppBarNative.GetMonitorInfo(hMonitor, ref mi)) return "未知";
        return $"{mi.rcMonitor.W}x{mi.rcMonitor.H} (物理), 工作区 {mi.rcWork.W}x{mi.rcWork.H}";
    }

    private static string TouchProbe()
    {
        var maxTouch = GetSystemMetrics(95 /*SM_MAXIMUMTOUCHES*/);
        return maxTouch > 0 ? $"触屏 ({maxTouch} 点)" : "无触屏";
    }

    [System.Runtime.InteropServices.LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int nIndex);
}
