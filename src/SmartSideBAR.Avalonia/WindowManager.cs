// 窗口集中管理 (方案 §4.1): 侧栏 AppBar 轨窗口的创建、常驻服务挂接与全部动作路由。
// 附录 A 的 window:*/floatball:*/capture:* 等 invoke 通道在本进程内直达类型安全服务。
using Avalonia;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Avalonia.Views;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Diagnostics;
using SmartSideBAR.Core.Links;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Audio;
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
    ISoundService sound,
    IDiagnosticsService diagnostics,
    SchedulerService scheduler,
    IEventBus bus,
    ILogger<WindowManager> log)
{
    // v1.2 设计基准 (DIP): rail 72 + 面板 380, dock 收起方块 52×52; 物理像素 = DIP × 窗口 DPI 缩放
    public const double RailWidthDip = 72;
    public const double PanelWidthDip = 380;
    public const double DockSizeDip = 52;

    private SidebarWindow? _sidebar;
    private DockWindow? _dockWindow;
    private FloatBallWindow? _floatBall;
    private SettingsWindow? _settings;
    private ReminderWindow? _reminder;
    private bool _docked;
    private AppBarEdge _side = AppBarEdge.Right;
    private (nint Hwnd, int RailPx, int ExpandedPx) _attachInfo;
    private bool _topmostKeeperActive;
    private EdgeTriggerWindow? _edgeTrigger;
    private MiniBoardWindow? _miniBoard;

    /// <summary>希沃式触发条模式（建议 #1）: edge=点击展开, edge-hover=悬停滑出。</summary>
    private bool IsEdgeTriggerMode
    {
        get
        {
            var mode = config.Current.Display.RailTrigger;
            return mode is "edge" or "edge-hover";
        }
    }

    public SidebarWindow CreateSidebar()
    {
        var cfg = config.Current;
        var side = cfg.Display.SidebarSide == SidebarSide.Left ? AppBarEdge.Left : AppBarEdge.Right;
        _side = side;

        // 面板体系需要设备/调度等服务（v1.2 各 Panel 经 IPC 取数，此处同进程直调）
        var services = new ViewModels.SidebarServices(config, appBar, ime, shell, usb, printer, scheduler, recorder, sound, links, Dispatch);
        // 把 AppBar 的窗口几何操作挪出 WndProc 调用栈（重入 shell 调用是本项目原生崩溃的来源之一）
        appBar.Defer = action =>
            global::Avalonia.Threading.Dispatcher.UIThread.Post(action, global::Avalonia.Threading.DispatcherPriority.Background);

        var win = new SidebarWindow(side, services);
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
            CrashLog.LastContext = new CrashContext(
                AppVersion, SafeMode.NormalizeDpi(scale), "none", SafeMode.OsBuildString());
            var railPx = (int)Math.Round(RailWidthDip * scale);
            var expandedPx = (int)Math.Round((RailWidthDip + PanelWidthDip) * scale);
            _attachInfo = (hwnd, railPx, expandedPx);

            // §5.4: AppBar 注册失败回退 alwaysOnTop (v1.2.0 同策略)
            var safeMode = SafeMode.FromEnvironment();
            if (safeMode)
            {
                log.LogWarning("[Window] 安全模式：跳过 AppBar 注册与悬浮球，仅保留侧栏本体");
                win.Topmost = true;
                StartTopmostKeeper(win);
            }
            else if (appBar.Attach(hwnd, side, railPx, expandedPx))
            {
                win.Topmost = false;
            }
            else
            {
                win.Topmost = true;
                StartTopmostKeeper(win);
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
            else if (!safeMode)
            {
                ShowFloatBall();
            }

            // 希沃式触发条模式 (建议 #1): 配置为 edge/edge-hover 时初始即收起为
            // 屏幕边缘极细触发条, 点击(或悬停)展开; 常驻胶囊场景不收起
            if (!safeMode && IsEdgeTriggerMode)
            {
                SetDocked(true);
            }

            // edge 模式: 指针离开且无面板打开 → 4s 后自动收回为触发条
            global::Avalonia.Threading.DispatcherTimer? edgeAutoDock = null;
            win.PointerExited += (_, _) =>
            {
                if (!IsEdgeTriggerMode) return;
                edgeAutoDock ??= new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                edgeAutoDock.Tick += (_, _) =>
                {
                    edgeAutoDock.Stop();
                    if (win.IsPointerOver || _docked) return;
                    if (win.DataContext is ViewModels.SidebarViewModel { PanelOpen: true }) return;
                    SetDocked(true);
                };
                edgeAutoDock.Start();
            };
            win.PointerEntered += (_, _) => edgeAutoDock?.Stop();
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

        // B7: 到点必须有可见反馈, 只播声音不够 (教师戴耳麦时听不见)
        bus.Subscribe<ReminderDue>(e =>
            ToastService.Instance.Show($"提醒: {e.Note ?? "时间到了"}", "warn"));

        // 几何事件: 面板宽度以系统授予为准 + 同步 Avalonia 窗口几何
        bus.Subscribe<AppBarGeometryChanged>(g =>
        {
            win.OnAppBarGeometry(g.Rect.W);
            // 关键: Avalonia 窗口管理器不知道 Win32 层 SetWindowPos 的尺寸/位置变更,
            // 必须同步 Avalonia Position/Width/Height, 否则窗口停留在 XAML 默认位置 (居中)。
            var scale = Windows.Native.Win32Display.GetScaling(_attachInfo.Hwnd);
            if (scale <= 0) scale = 1.0;
            win.Width = g.Rect.W / scale;
            win.Height = g.Rect.H / scale;
            win.Position = new PixelPoint(g.Rect.X, g.Rect.Y);
            log.LogDebug("[Window] AppBar 几何: ({X},{Y}) {W}x{H}px → {Wdip:F0}x{Hdip:F0} DIP",
                g.Rect.X, g.Rect.Y, g.Rect.W, g.Rect.H, win.Width, win.Height);
        });

        // K3: 全屏应用在场自动收缩为 dock 方块; 退出后自动恢复 (v1.1 同语义)
        // 全屏自适应：先延迟 1.2 s 再确认。系统通知在窗口切换/动画期会抖动，
        // 立即采信会让侧栏莫名其妙收缩（命令行、无边框工具窗口铺满屏幕时尤其明显）。
        bus.Subscribe<FullscreenAppChanged>(_ =>
        {
            if (!config.Current.Display.AutoCollapseOnFullscreen) return;
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() =>
            {
                if (!config.Current.Display.AutoCollapseOnFullscreen) return;
                var active = appBar.FullscreenActive;
                // P0-3: 排除名单命中时不收缩 (借鉴 ClassIsland 的 ExcludedFullscreenWindow)。
                // 资源管理器/无边框工具窗口常把最大化报成全屏, 现场可自行加名单。
                if (active && IsFullscreenExcluded(config.Current.Display.ExcludedFullscreenWindows))
                {
                    active = false;
                    log.LogDebug("[Window] 全屏应用命中排除名单 → 不收缩侧栏");
                }
                if (active == _docked) return;
                log.LogInformation(active
                    ? "[Window] 确认全屏应用在场 → 自动收缩侧栏"
                    : "[Window] 全屏应用已退出 → 自动展开侧栏");
                SetDocked(active);
            }, TimeSpan.FromMilliseconds(1200));
        });

        log.LogInformation("[Window] 侧栏窗口已创建 ({Side} 侧, rail={Rail} DIP, 胶囊高度=工作区 84%)", side, RailWidthDip);
        return win;
    }

    /// <summary>
    /// 全屏排除名单判定。名单为空则一律不排除; 取不到前台进程名时按"不排除"处理(保守不收缩)。
    /// </summary>
    private static bool IsFullscreenExcluded(IReadOnlyList<string>? excluded)
    {
        if (excluded is null || excluded.Count == 0) return false;
        var name = global::SmartSideBAR.Windows.AppBar.AppBarService.ForegroundProcessName();
        if (string.IsNullOrEmpty(name)) return false;
        return excluded.Any(x =>
            string.Equals(x?.Trim().ToLowerInvariant(), name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// KoBar topmostKeeper 策略移植: Windows 在显示切换/休眠唤醒/输入源切换后会悄悄
    /// 丢弃 always-on-top 的情况。教学一体机睡眠唤醒尤其频繁, Topmost 回退模式下每 30s
    /// 重申一次置顶 (toggle 触发 Win32 重新插入 topmost 带, 等价 KoBar 的 moveTop);
    /// AppBar 注册成功后自动停表。
    /// </summary>
    private void StartTopmostKeeper(SidebarWindow win)
    {
        if (_topmostKeeperActive) return;
        _topmostKeeperActive = true;
        // P0-1: 交给统一时钟驱动, 不再单独起一个 30s 定时器
        ClockHost.Register("topmost-keeper", 30_000, () =>
        {
            if (appBar.IsRegistered)
            {
                ClockHost.Unregister("topmost-keeper");
                _topmostKeeperActive = false;
                return;
            }
            if (_docked || !win.IsVisible) return;
            win.Topmost = false;
            win.Topmost = true;
        });
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
        hotkeys.Register("ime",
            string.IsNullOrWhiteSpace(cfg.Ime.Hotkey) ? "Ctrl+Shift+I" : cfg.Ime.Hotkey);
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

                case "miniboard":
                    ShowMiniBoard();
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

                case { } ws when ws.StartsWith("workspace:", StringComparison.OrdinalIgnoreCase):
                    SwitchWorkspace(ws["workspace:".Length..]);
                    break;

                case "reminder":
                    OpenReminder();
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
        new AnnotateWindow(background, config.Current.Capture.Dir, config.Current.Capture.Format,
            config.Current.Capture.InkToShape).Show();
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
        // B5: 传递麦克风配置 (读 config.recorder.mic)
        await recorder.StartAsync(new RecorderOptions(path, fps, 2000, Microphone: mic));
    }

    /// <summary>KoBar 工作区切换: 落盘 + 重建 rail（模块组合立即生效）。</summary>
    private void SwitchWorkspace(string name)
    {
        _ = config.Set("workspace.current", name);
        if (_sidebar?.DataContext is ViewModels.SidebarViewModel vm) vm.RebuildRail();
        ToastService.Instance.Show($"已切换到「{name}」工作区", "success");
    }

    /// <summary>崩溃上下文里的版本号（与 Directory.Build.props &lt;Version&gt; 同源）。</summary>
    private static string AppVersion =>
        typeof(WindowManager).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion
        ?? typeof(WindowManager).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    public void ShowFloatBall()
    {
        if (_floatBall is { IsVisible: true }) return;
        if (config.Current.Policy.DisabledModules.Contains("floatball", StringComparer.OrdinalIgnoreCase)) return;
        // R4: 悬浮球定位改用侧栏所在目标屏, 多显示器下主屏会偏
        var wa = GetSidebarWorkArea();
        var scale = Windows.Native.Win32Display.GetScaling(_attachInfo.Hwnd);
        _floatBall = new FloatBallWindow(config, config.Current.FloatBall,
            new Core.FloatBall.RectLike(wa.X, wa.Y, wa.W, wa.H), scale, Dispatch, ToggleSidebar);
        _floatBall.Show();
    }

    public void ToggleFloatBall()
    {
        if (_floatBall is null) { ShowFloatBall(); return; }
        if (_floatBall.IsVisible) _floatBall.Hide();
        else _floatBall.Show();
    }

    /// <summary>板中板 (ICC MiniWhiteboard 模式): 临时演算纸浮窗。重复唤起时置前而非新开。</summary>
    public void ShowMiniBoard()
    {
        if (_miniBoard is { IsVisible: true })
        {
            _miniBoard.Activate();
            return;
        }
        _miniBoard = new MiniBoardWindow(config.Current.Capture.Dir, config.Current.Capture.InkToShape);
        // 定位到侧栏所在屏的工作区居中 (多屏时不出现在教师不看的屏上)
        var wa = GetSidebarWorkArea();
        var scale = Windows.Native.Win32Display.GetScaling(_attachInfo.Hwnd);
        if (scale <= 0) scale = 1.0;
        var x = wa.X + Math.Max(0, (wa.W - (int)Math.Round(860 * scale)) / 2);
        var y = wa.Y + Math.Max(0, (wa.H - (int)Math.Round(560 * scale)) / 3);
        _miniBoard.Position = new PixelPoint(x, y);
        _miniBoard.Show();
        log.LogInformation("[Window] 板中板已打开 ({X},{Y})", x, y);
    }

    /// <summary>v1.1 dock/undock: 收起为工作区底角小方块 / 恢复玻璃胶囊。
    /// 勘误: 不复用侧栏窗口缩放 (透明窗外部 MoveWindow 缩放后 Avalonia 不出帧),
    /// dock 用独立 DockWindow; AppBar 先注销 (WorkArea 完整归还, v1.1 同语义),
    /// undock 时对侧栏窗口重新 Attach (含残留注册的自动清理)。</summary>
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
            var wa = GetSidebarWorkArea();
            var scale = Windows.Native.Win32Display.GetScaling(_attachInfo.Hwnd);
            if (scale <= 0) scale = 1.0;

            if (IsEdgeTriggerMode)
            {
                // 希沃式触发条: 屏幕边缘居中的极细竖条, 点击/悬停展开 (防鼠标误触)
                if (_edgeTrigger is null)
                {
                    _edgeTrigger = new EdgeTriggerWindow(() => SetDocked(false),
                        hoverToExpand: config.Current.Display.RailTrigger == "edge-hover", _side);
                }
                const int wDip = 14, hDip = 160;
                var x = _side == AppBarEdge.Right ? wa.X + wa.W - (int)Math.Round(wDip * scale) : wa.X;
                var y = wa.Y + (wa.H - (int)Math.Round(hDip * scale)) / 2;
                _edgeTrigger.Position = new PixelPoint(x, y);
                _edgeTrigger.Show();
                log.LogInformation("[Window] 侧栏已收起为边缘触发条 ({X},{Y})", x, y);
                return;
            }

            if (_dockWindow is null)
            {
                _dockWindow = new DockWindow(() => SetDocked(false));
            }
            // R4 修复: dock 定位也用目标屏而非主屏
            var sizePx = (int)Math.Round(DockSizeDip * scale);
            var dockX = _side == AppBarEdge.Right ? wa.X + wa.W - sizePx : wa.X;
            _dockWindow.Position = new PixelPoint(dockX, wa.Y + wa.H - sizePx);
            _dockWindow.Show();
            log.LogInformation("[Window] 侧栏已收起为底角方块 ({X},{Y}) {Size}px", dockX, wa.Y + wa.H - sizePx, sizePx);
        }
        else
        {
            _edgeTrigger?.Hide();
            _dockWindow?.Hide();
            sidebar.Show();
            // C4-⑤: fire-and-forget 改为同步调用并记录失败 (Attach 内部已有异常保护)
            try
            {
                appBar.Attach(_attachInfo.Hwnd, _side, _attachInfo.RailPx, _attachInfo.ExpandedPx);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "[Window] AppBar 重新 Attach 失败");
            }
            sidebar.Topmost = !appBar.IsRegistered; // Attach 失败回退置顶
            // B24: dock 期间 WorkArea 归还 / 窗口隐藏, undock 后 Avalonia 与系统几何会差一拍。
            // 事件循环空转后再协商一次, 把位置钉回显示器外缘 (否则栏位会内缩一个自身宽度)。
            global::Avalonia.Threading.DispatcherTimer.RunOnce(() =>
            {
                if (_docked) return;
                appBar.Reposition(appBar.CurrentWidthPx);
                var want = appBar.GrantedRect;
                if (want is { } w && (Math.Abs(sidebar.Position.X - w.X) > 2 || Math.Abs(sidebar.Position.Y - w.Y) > 2))
                {
                    log.LogWarning("[Window] undock 几何偏差已纠正: 实际 ({AX},{AY}) → 期望 ({EX},{EY})",
                        sidebar.Position.X, sidebar.Position.Y, w.X, w.Y);
                }
            }, TimeSpan.FromMilliseconds(180));
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

    /// <summary>第二次启动被唤醒时的反馈（单实例守卫回调，已在 UI 线程）。</summary>
    public void NotifyAlreadyRunning()
    {
        ToastService.Instance.Show("SmartSideBAR 已在运行", "info");
        if (_docked) SetDocked(false);
        _sidebar?.Show();
    }

    public void OpenSettings()
    {
        if (_settings is { IsVisible: true }) return;
        _settings = new SettingsWindow(config, shell, hotkeys, diagnostics);
        _settings.Closed += (_, _) =>
        {
            RegisterConfiguredHotkeys();   // 热键改动即时生效
            _sidebar?.ApplyTheme();        // 主题/强调色改动即时生效
        };
        _settings.Show();
    }

    /// <summary>定时提醒入口 (rail 铃铛 / 后续热键共用)。</summary>
    public void OpenReminder()
    {
        if (_reminder is { IsVisible: true }) return;
        _reminder = new ReminderWindow(scheduler);
        _reminder.Show();
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

    /// <summary>
    /// R4 修复: 获取侧栏所在显示器的工作区 (物理像素)。
    /// 早期固定用主屏 PrimaryWorkAreaPx, 多显示器时悬浮球/dock 会定位偏。
    /// 优先取侧栏窗口所在屏, 无侧栏时回退主屏。
    /// </summary>
    private (int X, int Y, int W, int H) GetSidebarWorkArea()
    {
        // P1-5: 显式指定目标显示器时优先按索引取(越界自动回退主屏)
        var monitorIndex = config.Current.Display.MonitorIndex;
        if (monitorIndex is >= 0)
        {
            try
            {
                return Windows.Native.Win32Display.WorkAreaByIndex(monitorIndex.Value);
            }
            catch { /* fallback */ }
        }

        // 尝试从侧栏 HWND 获取所在显示器的工作区
        if (_attachInfo.Hwnd != 0)
        {
            try
            {
                return Windows.Native.Win32Display.WorkAreaFromHwnd(_attachInfo.Hwnd);
            }
            catch { /* fallback */ }
        }
        return Windows.Native.Win32Display.PrimaryWorkAreaPx();
    }
}
