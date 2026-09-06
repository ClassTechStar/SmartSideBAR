// 窗口集中管理 (方案 §4.1): 侧栏 AppBar 轨窗口的创建、常驻服务挂接与全部动作路由。
// 附录 A 的 window:*/floatball:*/capture:* 等 invoke 通道在本进程内直达类型安全服务。
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Avalonia.Views;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Links;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Capture;
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Recording;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia;

public sealed class WindowManager(
    ConfigService config,
    LinksService links,
    AppBarService appBar,
    HotkeyService hotkeys,
    IImeService ime,
    IUsbWatcher usb,
    IPrinterMonitor printer,
    IShellService shell,
    ICaptureApi capture,
    ILongshotService longshot,
    IRecorderApi recorder,
    IDiagnosticsService diagnostics,
    IEventBus bus,
    ILogger<WindowManager> log)
{
    public const int RailWidthPx = 64;    // rail 收起宽度 (物理像素)
    public const int ExpandedWidthPx = 420; // 面板展开宽度 (物理像素)

    private SidebarWindow? _sidebar;
    private FloatBallWindow? _floatBall;
    private SettingsWindow? _settings;

    public SidebarWindow CreateSidebar()
    {
        var cfg = config.Current;
        var side = cfg.Display.SidebarSide == SidebarSide.Left ? AppBarEdge.Left : AppBarEdge.Right;

        var win = new SidebarWindow(side)
        {
            DataContext = new ViewModels.SidebarViewModel(appBar, ime, shell, links.List(), Dispatch),
        };
        _sidebar = win;
        win.Opened += (_, _) =>
        {
            var handle = win.TryGetPlatformHandle();
            if (handle is null)
            {
                log.LogWarning("[Window] 未能获取平台句柄, 侧栏以 Topmost 模式运行");
                win.Topmost = true;
                return;
            }
            var hwnd = handle.Handle;

            // §5.4: AppBar 注册失败回退 alwaysOnTop (v1.2.0 同策略)
            if (appBar.Attach(hwnd, side, RailWidthPx, ExpandedWidthPx))
            {
                win.Topmost = false;
            }
            else
            {
                win.Topmost = true;
                log.LogWarning("[Window] AppBar 注册失败 → Topmost 回退");
            }

            // ---- 常驻服务挂接 (同一 WndProcHook 基础设施) ----
            hotkeys.Attach(hwnd);
            RegisterConfiguredHotkeys();
            usb.Start(hwnd);
            printer.Start();

            // 首启 → OOBE
            if (!config.Current.Oobe.Completed && !config.Current.Oobe.Skipped)
            {
                ShowOobe();
            }
            else
            {
                ShowFloatBall();
            }
        };
        win.Closed += (_, _) => appBar.Detach();

        // 热键 → 动作
        bus.Subscribe<HotkeyPressed>(e => Dispatch(e.Slot));

        // 设备事件 → Toast
        bus.Subscribe<UsbArrived>(e => ToastService.Instance.Show($"U 盘已接入: {e.Info.Drive} {e.Info.Label ?? ""} {e.Info.SizeGb}GB", "info"));
        bus.Subscribe<UsbRemoved>(e => ToastService.Instance.Show($"U 盘已移除: {e.Info.Drive}", "warn"));
        bus.Subscribe<PrinterChanged>(e =>
        {
            var abnormal = e.Printers.Where(p => p.WireState is not "ok");
            foreach (var p in abnormal)
            {
                ToastService.Instance.Show($"打印机「{p.Name}」状态: {p.WireState}", "warn");
            }
        });
        bus.Subscribe<RecorderStatusChanged>(e =>
            ToastService.Instance.Show(e.Recording ? $"开始录屏: {e.FilePath}" : $"录屏完成: {e.FilePath}", e.Recording ? "info" : "success"));
        bus.Subscribe<LongshotCompleted>(e =>
            ToastService.Instance.Show(e.Error is null ? $"长截图完成: {e.FilePath}" : $"长截图失败: {e.Error}", e.Error is null ? "success" : "error"));

        // 几何事件: 面板宽度以系统授予为准
        bus.Subscribe<AppBarGeometryChanged>(g =>
        {
            win.OnAppBarGeometry(g.Rect.W);
            log.LogDebug("[Window] AppBar 几何: ({X},{Y}) {W}x{H}px", g.Rect.X, g.Rect.Y, g.Rect.W, g.Rect.H);
        });

        log.LogInformation("[Window] 侧栏窗口已创建 ({Side} 侧, rail={Rail}px)", side, RailWidthPx);
        return win;
    }

    public void RegisterConfiguredHotkeys()
    {
        var cfg = config.Current;
        if (!config.Current.Policy.DisabledModules.Contains("capture", StringComparer.OrdinalIgnoreCase))
        {
            hotkeys.Register("capture", cfg.Capture.Hotkey);
        }
        if (!config.Current.Policy.DisabledModules.Contains("annotate", StringComparer.OrdinalIgnoreCase))
        {
            hotkeys.Register("annotate", cfg.Capture.AnnotateHotkey);
        }
        if (!config.Current.Policy.DisabledModules.Contains("longshot", StringComparer.OrdinalIgnoreCase))
        {
            hotkeys.Register("longshot", cfg.Capture.LongshotHotkey);
        }
        hotkeys.Register("floatball", cfg.FloatBall.Hotkey);
        hotkeys.Register("ime", "Ctrl+Shift+I");
    }

    /// <summary>动作总路由: rail 按钮 / 悬浮球 / 热键 / 托盘共用。</summary>
    public void Dispatch(string action)
    {
        try
        {
            switch (action)
            {
                case "capture" or "capture-hotkey":
                    EnsureCaptureDirs();
                    new OverlayWindow(capture, config.Current.Capture.Dir, config.Current.Capture.Format).Show();
                    break;

                case "annotate" or "annotate-hotkey":
                    EnsureCaptureDirs();
                    StartAnnotate();
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
                    ToggleFloatBall();
                    break;

                case "sidebar-toggle":
                    ToggleSidebar();
                    break;

                case "settings":
                    OpenSettings();
                    break;

                case "usb" or "printer":
                    ShowDiagnostics(action);
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
        new AnnotateWindow(background, config.Current.Capture.Dir, config.Current.Capture.Format).Show();
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
        var path = Path.Combine(dir, $"record_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        await recorder.StartAsync(new RecorderOptions(path, fps, 2000));
    }

    public void ShowFloatBall()
    {
        if (_floatBall is { IsVisible: true }) return;
        if (config.Current.Policy.DisabledModules.Contains("floatball", StringComparer.OrdinalIgnoreCase)) return;
        _floatBall = new FloatBallWindow(config.Current.FloatBall, new Core.FloatBall.RectLike(0, 0, 2560, 1600), Dispatch);
        _floatBall.Show();
    }

    public void ToggleFloatBall()
    {
        if (_floatBall is null) { ShowFloatBall(); return; }
        if (_floatBall.IsVisible) _floatBall.Hide();
        else _floatBall.Show();
    }

    public void ToggleSidebar()
    {
        if (_sidebar is { } win)
        {
            if (!win.IsVisible)
            {
                win.Show();
                win.WindowState = WindowState.Normal;
            }
            else
            {
                win.Hide();
            }
        }
    }

    public void OpenSettings()
    {
        if (_settings is { IsVisible: true }) return;
        _settings = new SettingsWindow(config, shell, hotkeys, diagnostics);
        _settings.Closed += (_, _) => RegisterConfiguredHotkeys(); // 热键改动即时生效
        _settings.Show();
    }

    public void ShowOobe()
    {
        var oobe = new OobeWindow(config, ime, printer);
        oobe.Closed += (_, _) => ShowFloatBall();
        oobe.Show();
    }

    /// <summary>设备按钮 → 轻量状态弹窗 (usb/printer 即时查询)。</summary>
    private void ShowDiagnostics(string kind)
    {
        var info = kind == "usb"
            ? string.Join('\n', usb.ListRemovable().Select(d => $"{d.Drive}  {d.Label ?? "(无卷标)"}  {d.SizeGb}GB")) is { Length: > 0 } s ? s : "未检测到可移动盘"
            : string.Join('\n', printer.Query().Select(p => $"{p.Name}: {p.WireState}")) is { Length: > 0 } s2 ? s2 : "无打印机";
        ToastService.Instance.Show(info, "info");
    }
}
