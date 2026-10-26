// §5.3 生命周期 + 通知状态机 (v1.2.0 缺口 K1/K2/K3/K4/K7 修复)
// 通知链: 系统 → WndProcHook(子类化) → OnWndProc 状态机 → Reposition/EventBus。
// 坐标 (K4): 与系统协商及窗口定位全链路物理像素, MoveWindow 直达, 无 DIP 换算。
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Windows.AppBar;

// ---- EventBus 事件 (UI 层订阅以同步面板布局) ----
public sealed record AppBarGeometryChanged(AppBarRect Rect, int WidthPx);
public sealed record FullscreenAppChanged(bool Active);
public sealed record WindowArrangePending(bool Hiding);

/// <summary>侧栏应用栏编排器。
/// 生命周期: Attach(自动恢复+注册+首次定位) ⇄ OnAppBarNotification(自适应) → Detach(注销)。</summary>
public sealed class AppBarService(
    IAppBarApi api,
    IWndProcHook hook,
    IEventBus bus,
    ILogger<AppBarService>? log = null,
    Func<bool?>? fullscreenProbe = null) : IDisposable
{
    /// <summary>v1.1 悬浮玻璃胶囊: 上下各留 8% 工作区高度 (不顶到屏幕上下缘)。</summary>
    public const double CapsuleMarginRatio = 0.08;

    private nint _hwnd;
    private uint _edge = AppBarNative.ABE_RIGHT;
    private int _railPx = 64;      // rail 收起宽度 (物理像素)
    private int _expandedPx = 420; // 面板展开宽度 (物理像素)
    private bool _registered;
    private bool _fullscreen;      // K3: 全屏应用在场标志
    private bool _userExpanded;    // 用户偏好; 全屏结束后据此恢复 (V5)
    private bool _docked;          // v1.1 dock 语义: 收起为角落小方块
    private readonly Func<bool?> _fullscreenProbe = fullscreenProbe ?? new Func<bool?>(ProbeForegroundFullscreen);

    public bool IsRegistered => _registered;
    public AppBarRect? GrantedRect { get; private set; }
    public bool FullscreenActive => _fullscreen;
    public int CurrentWidthPx => _userExpanded ? _expandedPx : _railPx;
    public bool Docked => _docked;

    /// <summary>注册 + 首次定位。失败返回 false (调用方回退 Topmost 置顶, 与 v1.2.0 同策略)。</summary>
    public bool Attach(nint hwnd, AppBarEdge edge, int railWidthPx, int expandedWidthPx)
    {
        if (_registered) Detach();
        _hwnd = hwnd;
        _edge = Map(edge);
        _railPx = Math.Max(2, railWidthPx);
        _expandedPx = Math.Max(_railPx, expandedWidthPx);
        _userExpanded = false;
        _fullscreen = false;

        // V6: 上次强杀可能残留注册, ABM_REMOVE 对未注册 hwnd 无副作用, 先补发一次
        api.Remove(hwnd);

        if (!api.Register(hwnd, AppBarNative.AppBarCallbackMsg))
        {
            log?.LogWarning("[AppBar] 注册失败 (可能与他栏冲突), 回退 alwaysOnTop 模式");
            return false;
        }
        _registered = true;

        // K1/K2/K3: 挂接 WndProc, v1.2.0 完全缺失的通知处理
        hook.Add(hwnd, OnWndProc);
        Reposition(CurrentWidthPx);
        log?.LogInformation("[AppBar] 已注册 edge={Edge} rail={Rail}px expanded={Expanded}px",
            edge, _railPx, _expandedPx);
        return true;
    }

    /// <summary>展开/收起 (window:resize 通道落点)。</summary>
    public void SetExpanded(bool expanded)
    {
        _userExpanded = expanded;
        Reposition(CurrentWidthPx);
    }

    /// <summary>
    /// B4: 自动隐藏 AppBar, 注册 ABM_SETAUTOHIDEBAR, 全屏时不遮挡, 鼠标贴边唤出。
    /// 这是 913dc35 用户抱怨「右侧屏幕被 AppBar 占用」的正面解:
    /// 既不偷 WorkArea, 又能在全屏应用下通过贴边唤出。
    /// </summary>
    public bool SetAutoHide(bool enable)
    {
        if (!_registered || _hwnd == 0) return false;
        _autoHide = enable;
        var ok = api.SetAutoHideBar(_hwnd, _edge, enable);
        if (ok)
        {
            log?.LogInformation("[AppBar] 自动隐藏已{State}", enable ? "启用" : "禁用");
        }
        else
        {
            log?.LogWarning("[AppBar] 自动隐藏设置失败");
        }
        return ok;
    }
    private bool _autoHide;

    /// <summary>v1.1 dock 语义: docked = 指定边距尺寸的小方块停靠在工作区底角。</summary>
    public void SetDocked(bool docked, int dockSizePx)
    {
        _docked = docked;
        _dockSizePx = Math.Max(2, dockSizePx);
        Reposition(CurrentWidthPx);
    }
    private int _dockSizePx = 78;

    /// <summary>重排: 注册后 / 展开收起 / ABN_POSCHANGED / 分辨率与任务栏变化 (K1/K4)。
    /// 锚点必须是 rcMonitor 全屏矩形: rcWork 已扣除自身 AppBar 占位, 若以它为锚,
    /// 每次 Reposition 都会向屏幕内侧漂移一个带宽, 这是对方案 §5.3 蓝本的勘误。
    /// 垂直范围 = 工作区高度 84% 的悬浮胶囊 (上下各留 8%, 对齐 v1.1 玻璃胶囊设计);
    /// 水平锚定显示器边缘, 沿边裁剪交给 ABM_QUERYPOS。</summary>
    /// <summary>
    /// 把一个动作挪出 WndProc 调用栈。默认同步（单测直接调用 Reposition 时行为不变）；
    /// Avalonia 侧注入 Dispatcher.Post。理由：在窗口过程里同步做
    /// ABM_REMOVE / ABM_SETPOS / MoveWindow 是与 shell 的重入式交互，偶发原生崩溃
    /// （无托管异常、无崩溃日志，进程直接消失）。
    /// </summary>
    public Action<Action>? Defer { get; set; }

    private bool _inHook;

    private void DeferReposition(int widthPx) => Dispatch(() => Reposition(widthPx));

    private void Dispatch(Action action)
    {
        if (Defer is { } post && _inHook) post(action);
        else action();
    }

    public void Reposition(int widthPx)
    {
        if (!_registered || _hwnd == 0) return;
        var mon = api.GetMonitorOf(_hwnd);

        // v1.1 dock 语义: 工作区底角小方块 (side-aware)
        if (_docked && (_edge is AppBarNative.ABE_LEFT or AppBarNative.ABE_RIGHT))
        {
            // 先注销自身旧占位: QUERYPOS 会为已注册 AppBar(含我们自己)让位, 否则方块逐次左漂。
            // ABM_REMOVE 后系统 WorkArea 恢复滞后一步, 收敛循环直至授予位==期望位。
            var dockWant = _edge == AppBarNative.ABE_RIGHT
                ? new AppBarRect(mon.Work.X + mon.Work.W - _dockSizePx,
                    mon.Work.Y + mon.Work.H - _dockSizePx, _dockSizePx, _dockSizePx)
                : new AppBarRect(mon.Work.X, mon.Work.Y + mon.Work.H - _dockSizePx, _dockSizePx, _dockSizePx);
            var dockGranted = dockWant;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                api.Remove(_hwnd);
                dockGranted = api.QueryAndSetPos(_hwnd, _edge, dockWant);
                if (Math.Abs(dockGranted.X - dockWant.X) <= 2 && Math.Abs(dockGranted.Y - dockWant.Y) <= 2)
                {
                    break;
                }
            }
            api.WindowPosChanged(_hwnd);
            if (!api.MoveWindow(_hwnd, dockGranted))
            {
                log?.LogWarning("[AppBar] MoveWindow 失败 (hwnd=0x{Hwnd:X})", _hwnd);
            }
            GrantedRect = dockGranted;
            bus.Publish(new AppBarGeometryChanged(dockGranted, _dockSizePx));
            log?.LogInformation("[AppBar] dock 重排 → ({X},{Y}) {W}x{H}px", dockGranted.X, dockGranted.Y, dockGranted.W, dockGranted.H);
            return;
        }

        var capsuleH = (int)Math.Round(mon.Work.H * (1 - 2 * CapsuleMarginRatio));
        var capsuleY = mon.Work.Y + (int)Math.Round(mon.Work.H * CapsuleMarginRatio);
        var want = _edge switch
        {
            AppBarNative.ABE_LEFT => new AppBarRect(mon.Monitor.X, capsuleY, widthPx, capsuleH),
            AppBarNative.ABE_RIGHT => new AppBarRect(mon.Monitor.X + mon.Monitor.W - widthPx, capsuleY, widthPx, capsuleH),
            AppBarNative.ABE_TOP => new AppBarRect(mon.Monitor.X, mon.Monitor.Y, mon.Monitor.W, widthPx),
            _ => new AppBarRect(mon.Monitor.X, mon.Monitor.Y + mon.Monitor.H - widthPx, mon.Monitor.W, widthPx),
        };
        var granted = api.QueryAndSetPos(_hwnd, _edge, want); // 系统审批 (避让任务栏/他栏)
        // B24  ABM_REMOVE 之后系统 WorkArea 的恢复滞后一拍。若侧栏刚从 dock 形态恢复,
        // 首次 QUERYPOS 仍会按「旧占位」把区间向内裁掉一个自身宽度 (实测 108px),
        // 之后再 SETPOS 就把这条缝永久留了下来。未贴边 → 注销并重注册后再协商一次。
        for (var attempt = 0; attempt < 3 && !IsAnchored(granted, mon.Monitor); attempt++)
        {
            api.Remove(_hwnd);
            if (!api.Register(_hwnd, AppBarNative.AppBarCallbackMsg)) break;
            granted = api.QueryAndSetPos(_hwnd, _edge, want);
            log?.LogInformation("[AppBar] 贴边重协商 #{Attempt} → ({X},{Y}) {W}x{H}px",
                attempt + 1, granted.X, granted.Y, granted.W, granted.H);
        }
        api.WindowPosChanged(_hwnd);                          // K2: 位置变更上报
        if (!api.MoveWindow(_hwnd, granted))                  // K4: 物理像素直达, 无 DIP 换算
        {
            log?.LogWarning("[AppBar] MoveWindow 失败 (hwnd=0x{Hwnd:X})", _hwnd);
        }
        GrantedRect = granted;
        bus.Publish(new AppBarGeometryChanged(granted, widthPx));
        log?.LogInformation("[AppBar] 重排 → ({X},{Y}) {W}x{H}px", granted.X, granted.Y, granted.W, granted.H);
    }

    /// <summary>WndProc 状态机, K1/K2/K3/K7 的通知处理。</summary>
    private nint OnWndProc(nint hwnd, uint msg, nuint wParam, nint lParam, ref bool handled)
    {
        _inHook = true;
        try
        {
            return OnWndProcCore(hwnd, msg, wParam, lParam, ref handled);
        }
        finally
        {
            _inHook = false;
        }
    }

    private nint OnWndProcCore(nint hwnd, uint msg, nuint wParam, nint lParam, ref bool handled)
    {
        if (msg == AppBarNative.AppBarCallbackMsg)
        {
            handled = true;
            switch (wParam)
            {
                case AppBarNative.ABN_POSCHANGED: // 任务栏换边/他栏变化/WorkArea 变
                    DeferReposition(CurrentWidthPx);
                    break;

                case AppBarNative.ABN_FULLSCREENAPP: // 授课视频等全屏应用开/关 (K3)
                                                     // K3 勘误: 系统对「最大化窗口」同样上报 TRUE, 直接采信会让侧栏在普通桌面
                                                     // 使用中被反复收进角落。此处用前台窗口实际几何复核, 只认真正铺满显示器的窗口。
                    _fullscreen = lParam != 0 && (_fullscreenProbe() ?? true);
                    if (_fullscreen) log?.LogInformation("[AppBar] 判定全屏: 前台窗口 {Class}", DescribeForeground());
                    bus.Publish(new FullscreenAppChanged(_fullscreen));
                    // 策略: 全屏在场保持注册 (教师仍可唤出) 但先收起为 rail; 关闭后按用户偏好恢复 (V5)
                    DeferReposition(_fullscreen ? _railPx : CurrentWidthPx);
                    break;

                case AppBarNative.ABN_STATECHANGE: // 任务栏「自动隐藏/总在顶部」切换
                    DeferReposition(CurrentWidthPx);
                    break;

                case AppBarNative.ABN_WINDOWARRANGE: // 层叠/平铺: TRUE=即将排列 FALSE=完成
                    bus.Publish(new WindowArrangePending(wParam != 0));
                    break;
            }
            return 0;
        }

        if (!_registered) return 0;
        if (msg == AppBarNative.WM_ACTIVATE)
        {
            api.Activate(_hwnd); // K2: 激活状态上报 (多 AppBar z 序维护)
        }
        else if (msg is AppBarNative.WM_DISPLAYCHANGE or AppBarNative.WM_DPICHANGED)
        {
            DeferReposition(CurrentWidthPx); // K7: 分辨率/DPI 变化重协商 (ABN_POSCHANGED 通常已带一次, 幂等)
        }
        return 0; // handled=false → 其余消息交还原窗口过程
    }

    /// <summary>退出/窗口销毁前必须调用, 否则系统 WorkArea 泄漏 (v1.2.0 语义保留)。</summary>
    public void Detach()
    {
        if (!_registered) return;
        hook.Remove(_hwnd, OnWndProc);
        api.Remove(_hwnd);
        _registered = false;
        log?.LogInformation("[AppBar] 已注销, 系统 WorkArea 恢复");
    }

    public void Dispose() => Detach();

    /// <summary>授予区间是否真的贴在显示器外缘 (LEFT/RIGHT 判 X, TOP/BOTTOM 判 Y)。</summary>
    private bool IsAnchored(AppBarRect granted, AppBarRect monitor)
    {
        const int tol = 2;
        return _edge switch
        {
            AppBarNative.ABE_LEFT => Math.Abs(granted.X - monitor.X) <= tol,
            AppBarNative.ABE_RIGHT => Math.Abs(granted.X + granted.W - (monitor.X + monitor.W)) <= tol,
            AppBarNative.ABE_TOP => Math.Abs(granted.Y - monitor.Y) <= tol,
            _ => Math.Abs(granted.Y + granted.H - (monitor.Y + monitor.H)) <= tol,
        };
    }

    /// <summary>前台窗口类名, 排查"误判全屏"时用 (无边框工具窗口也会铺满屏幕)。</summary>
    private static string DescribeForeground()
    {
        var fg = AppBarNative.GetForegroundWindow();
        if (fg == 0) return "?";
        Span<char> cls = stackalloc char[128];
        var n = AppBarNative.GetClassName(fg, cls, cls.Length);
        return n > 0 ? new string(cls[..n]) : "?";
    }

    /// <summary>
    /// 前台窗口所属进程名(小写, 无扩展名); 取不到时返回 null。
    /// 供"全屏排除名单"使用 (借鉴 ClassIsland 的 ExcludedFullscreenWindow)。
    /// </summary>
    public static string? ForegroundProcessName()
    {
        var fg = AppBarNative.GetForegroundWindow();
        if (fg == 0) return null;
        _ = AppBarNative.GetWindowThreadProcessId(fg, out var pid);
        if (pid == 0) return null;
        try
        {
            using var p = System.Diagnostics.Process.GetProcessById((int)pid);
            return p.ProcessName?.ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>前台窗口是否真的铺满其所在显示器 (最大化窗口不算)。
    /// 返回 null = 无法判定 (无前台窗口 / 查询失败), 调用方沿用系统上报。</summary>
    private static bool? ProbeForegroundFullscreen()
    {
        using var scope = new AppBarApi.PhysicalDpiScope();
        var fg = AppBarNative.GetForegroundWindow();
        if (fg == 0) return null;
        // 自家窗口（截图/批注覆盖层本身也是全屏）不得判定为"全屏应用"，
        // 否则一开覆盖层就把侧栏收进角落、面板被强制关闭。
        _ = AppBarNative.GetWindowThreadProcessId(fg, out var fgPid);
        if (fgPid == (uint)Environment.ProcessId) return false;
        if ((AppBarNative.GetWindowLongPtr(fg, AppBarNative.GWL_STYLE).ToInt64() & AppBarNative.WS_MAXIMIZE) != 0) return false;
        if (!AppBarNative.GetWindowRect(fg, out var wr)) return null;
        var hMon = AppBarNative.MonitorFromWindow(fg, AppBarNative.MONITOR_DEFAULTTONEAR);
        var mi = new AppBarNative.MONITORINFO
        {
            cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<AppBarNative.MONITORINFO>(),
        };
        if (!AppBarNative.GetMonitorInfo(hMon, ref mi)) return null;
        return Math.Abs(wr.Left - mi.rcMonitor.Left) <= 2
            && Math.Abs(wr.Top - mi.rcMonitor.Top) <= 2
            && Math.Abs(wr.Right - mi.rcMonitor.Right) <= 2
            && Math.Abs(wr.Bottom - mi.rcMonitor.Bottom) <= 2;
    }

    private static uint Map(AppBarEdge edge) => edge switch
    {
        AppBarEdge.Left => AppBarNative.ABE_LEFT,
        AppBarEdge.Top => AppBarNative.ABE_TOP,
        AppBarEdge.Right => AppBarNative.ABE_RIGHT,
        _ => AppBarNative.ABE_BOTTOM,
    };
}
