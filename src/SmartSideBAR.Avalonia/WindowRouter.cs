// 动作总路由 (P1-9 从 WindowManager 拆出): rail 按钮 / 悬浮球 / 热键 / 托盘共用。
// 窗口生命周期/几何仍在 WindowManager; 这里只管「动作 → 服务调用 / 窗口唤起」。
using Microsoft.Extensions.Logging;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Core.Telemetry;
using SmartSideBAR.Windows.Capture;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Recording;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia;

internal sealed class WindowRouter(
    WindowManager host,
    ConfigService config,
    LessonStatsService stats,
    ICaptureApi capture,
    ILongshotService longshot,
    IRecorderApi recorder,
    IImeService ime,
    IShellService shell,
    IEventBus bus,
    ILogger<WindowManager> log)
{
    /// <summary>动作总路由: rail 按钮 / 悬浮球 / 热键 / 托盘共用。</summary>
    public void Dispatch(string action)
    {
        try
        {
            switch (action)
            {
                case "capture" or "capture-hotkey":
                    EnsureCaptureDirs();
                    stats.Record("capture");
                    new OverlayWindow(capture, config.Current.Capture.Dir, config.Current.Capture.Format).Show();
                    break;

                case "annotate" or "annotate-hotkey":
                    EnsureCaptureDirs();
                    StartAnnotate();
                    break;

                case "miniboard":
                    host.ShowMiniBoard();
                    break;

                case "longshot" or "longshot-hotkey":
                    StartLongshotPicker();
                    break;

                case "record" or "record-hotkey":
                    _ = ToggleRecorderAsync();
                    break;

                case "ime":
                    _ = ime.Toggle();
                    break;

                case "taskmgr":
                    shell.OpenTaskManager();
                    break;

                case "sidebar" or "floatball":
                    host.ToggleFloatBall();
                    break;

                case "sidebar-toggle":
                    host.ToggleSidebar();
                    break;

                case "dock":
                    host.SetDocked(!host.IsDocked);
                    break;

                case "settings":
                    host.OpenSettings();
                    break;

                case { } ws when ws.StartsWith("workspace:", StringComparison.OrdinalIgnoreCase):
                    host.SwitchWorkspace(ws["workspace:".Length..]);
                    break;

                case "reminder":
                    host.OpenReminder();
                    break;

                case "usb" or "printer":
                    host.ShowDiagnostics(action);
                    break;

                default:
                    log.LogWarning("[Window] 未知动作: {Action}", action);
                    break;
            }
        }
        catch (Exception ex)
        {
            log.LogError(ex, "[Window] 动作执行失败: {Action}", action);
            ToastService.Instance.Show($"{action} 失败: {ex.Message}", "error");
        }
    }

    private void EnsureCaptureDirs()
    {
        Directory.CreateDirectory(config.Current.Capture.Dir);
    }

    private void StartAnnotate()
    {
        byte[]? background = null;
        try
        {
            // 打开瞬间抓全屏底图 (WithBackground 导出用); 失败允许仅笔迹
            using var bmp = capture.CaptureMonitor(0);
            if (bmp is not null)
            {
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                background = ms.ToArray();
            }
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "[Window] 批注底图抓取失败");
        }
        new AnnotateWindow(background, config.Current.Capture.Dir, config.Current.Capture.Format,
            config.Current.Capture.InkToShape, bus).Show();
    }

    private void StartLongshotPicker()
    {
        var targets = longshot.EnumTargets();
        if (targets.Count == 0)
        {
            ToastService.Instance.Show("未找到可截图的窗口", "warn");
            return;
        }
        var picker = new WindowPickerWindow(targets);
        picker.Closed += (_, _) =>
        {
            if (picker.Selected is { } target)
            {
                _ = Task.Run(() => longshot.CaptureAsync(target, config.Current.Capture.Dir, config.Current.Capture.Format));
            }
        };
        picker.Show();
    }

    private async Task ToggleRecorderAsync()
    {
        if (!config.Current.Recorder.Enabled)
        {
            ToastService.Instance.Show("录屏已在设置中关闭（本机媒体管道不可用）", "warn");
            return;
        }
        if (recorder.IsRecording)
        {
            await recorder.StopAsync();
            return;
        }
        if (!recorder.IsSupported())
        {
            ToastService.Instance.Show("此系统不支持录屏 (需 Win10 1803+)", "error");
            return;
        }
        var dir = config.Current.Recorder.Dir;
        Directory.CreateDirectory(dir);
        var fps = Math.Clamp(config.Current.Recorder.Fps, 5, 60);
        var mic = config.Current.Recorder.Mic;
        var path = Path.Combine(dir, $"record_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        stats.Record("record"); // 发起点 (时长由完成事件补记)
        // B5: 传递麦克风配置 (读 config.recorder.mic)
        await recorder.StartAsync(new RecorderOptions(path, fps, 2000, Microphone: mic));
    }
}
