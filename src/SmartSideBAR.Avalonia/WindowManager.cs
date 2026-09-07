// 窗口集中管理 (方案 §4.1): 侧栏 AppBar 轨窗口的创建、常驻服务挂接与全部动作路由。
// 附录 A 的 window:*/floatball:*/capture:* 等 invoke 通道在本进程内直达类型安全服务。
using Avalonia;
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
    // v1.1 设计基准 (DIP): rail 52 + 面板 380, dock 收起方块 52×52; 物理像素 = DIP × 窗口 DPI 缩放
    public const double RailWidthDip = 52;
    public const double PanelWidthDip = 380;
    public const double DockSizeDip = 52;

    private SidebarWindow? _sidebar;
    private DockWindow? _dockWindow;
    private FloatBallWindow? _floatBall;
    private SettingsWindow? _settings;
    private int _dockPx = 78;
    private bool _docked;
    private AppBarEdge _side = AppBarEdge.Right;
    private (nint Hwnd, int RailPx, int ExpandedPx) _attachInfo;

    public SidebarWindow CreateSidebar()
    {
        var cfg = config.Current;
        var side = cfg.Display.SidebarSide == SidebarSide.Left ? AppBarEdge.Left : AppBarEdge.Right;
        _side = side;

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
            var scale = Windows.Native.Win32Display.GetScaling(hwnd);
            var railPx = (int)Math.Round(RailWidthDip * scale);
            var expandedPx = (int)Math.Round((RailWidthDip + PanelWidthDip) * scale);
            _dockPx = (int)Math.Round(DockSizeDip * scale);
            _attachInfo = (hwnd, railPx, expandedPx);

            // §5.4: AppBar 注册失败回退 alwaysOnTop (v1.2.0 同策略)
            if (appBar.Attach(hwnd, side, railPx, expandedPx))
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

        // K3: 全屏应用在场自动收缩为 dock 方块; 退出后自动恢复 (v1.1 同语义)
        bus.Subscribe<FullscreenAppChanged>(e =>
        {
            if (e.Active)
            {
                if (!_docked)
                {
                    log.LogInformation("[Window] 检测到全屏应用 → 自动收缩侧栏");
                    SetDocked(true);
                }
            }
            else
            {
                if (_docked)
                {
                    log.LogInformation("[Window] 全屏应用退出 → 自动展开侧栏");
                    SetDocked(false);
                }
            }
        });

        log.LogInformation("[Window] 侧栏窗口已创建 ({Side} 侧, rail={Rail} DIP, 胶囊高度=工作区 84%)", side, RailWidthDip);
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

                case "dock":
                    SetDocked(!_docked);
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
        var wa = Windows.Native.Win32Display.PrimaryWorkAreaPx();
        _floatBall = new FloatBallWindow(config, config.Current.FloatBall,
            new Core.FloatBall.RectLike(wa.X, wa.Y, wa.W, wa.H), Dispatch, ToggleSidebar);
        _floatBall.Show();
    }

    public void ToggleFloatBall()
    {
        if (_floatBall is null) { ShowFloatBall(); return; }
        if (_floatBall.IsVisible) _floatBall.Hide();
        else _floatBall.Show();
    }

    /// <summary>v1.1 dock/undock: 收起为工作区底角小方块 / 恢复玻璃胶囊。
    /// 勘误: 不复用侧栏窗口缩放 (透明窗外部 MoveWindow 缩放后 Avalonia 不出帧),
    /// dock 用独立 DockWindow; AppBar 先注销 (WorkArea 完整归还, v1.1 同语义),
    /// undock 时对侧栏窗口重新 Attach (含孤儿注册自愈)。</summary>
    public void SetDocked(bool docked)
    {
        if (docked == _docked) return;
        _docked = docked;
        if (_sidebar is not { } sidebar) return;
        if (sidebar.DataContext is ViewModels.SidebarViewModel vm) vm.IsDocked = docked;

        if (docked)
        {
            appBar.Detach();
            sidebar.Hide();
            if (_dockWindow is null)
            {
                _dockWindow = new DockWindow(() => SetDocked(false));
            }
            var wa = Windows.Native.Win32Display.PrimaryWorkAreaPx();
            var sizePx = (int)Math.Round(DockSizeDip * Windows.Native.Win32Display.GetScaling(_attachInfo.Hwnd));
            var x = _side == AppBarEdge.Right ? wa.X + wa.W - sizePx : wa.X;
            _dockWindow.Position = new PixelPoint(x, wa.Y + wa.H - sizePx);
            _dockWindow.Show();
            log.LogInformation("[Window] 侧栏已收起为底角方块 ({X},{Y}) {Size}px", x, wa.Y + wa.H - sizePx, sizePx);
        }
        else
        {
            _dockWindow?.Hide();
            sidebar.Show();
            _ = appBar.Attach(_attachInfo.Hwnd, _side, _attachInfo.RailPx, _attachInfo.ExpandedPx);
            sidebar.Topmost = !appBar.IsRegistered; // Attach 失败回退置顶
            log.LogInformation("[Window] 侧栏已展开为玻璃胶囊");
        }
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
