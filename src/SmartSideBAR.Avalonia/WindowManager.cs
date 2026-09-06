// 窗口集中管理 (方案 §4.1): 侧栏 AppBar 轨窗口的创建、常驻服务挂接与动作路由。
// 坐标 (K4): Avalonia 侧不自行定位 —— 窗口位置由 AppBarService 经 MoveWindow 以物理像素下达。
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Avalonia.Views;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Links;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Input;
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
    IEventBus bus,
    ILogger<WindowManager> log)
{
    /// <summary>rail 收起宽度 (物理像素) —— 方案 §5.3 默认</summary>
    public const int RailWidthPx = 64;
    /// <summary>面板展开宽度 (物理像素)</summary>
    public const int ExpandedWidthPx = 420;

    private SidebarWindow? _sidebar;

    public SidebarWindow CreateSidebar()
    {
        var cfg = config.Current;
        var side = cfg.Display.SidebarSide == SidebarSide.Left ? AppBarEdge.Left : AppBarEdge.Right;

        var win = new SidebarWindow(side)
        {
            DataContext = new ViewModels.SidebarViewModel(appBar, ime, shell, links.List()),
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

            // §5.4: AppBar 模式下不置顶 (系统保证不被最大化遮盖); 注册失败才回退 alwaysOnTop
            if (appBar.Attach(hwnd, side, RailWidthPx, ExpandedWidthPx))
            {
                win.Topmost = false;
            }
            else
            {
                win.Topmost = true; // v1.2.0 同款 fallback
                log.LogWarning("[Window] AppBar 注册失败 → Topmost 回退");
            }

            // ---- Wave B 常驻服务挂接 (全部经同一 WndProcHook 基础设施) ----
            hotkeys.Attach(hwnd);
            RegisterConfiguredHotkeys();
            usb.Start(hwnd);
            printer.Start();
        };
        win.Closed += (_, _) => appBar.Detach();

        // 几何事件: 面板内容宽度同步以系统授予为准
        bus.Subscribe<AppBarGeometryChanged>(g =>
        {
            win.OnAppBarGeometry(g.Rect.W);
            log.LogDebug("[Window] AppBar 几何: ({X},{Y}) {W}x{H}px", g.Rect.X, g.Rect.Y, g.Rect.W, g.Rect.H);
        });

        log.LogInformation("[Window] 侧栏窗口已创建 ({Side} 侧, rail={Rail}px)", side, RailWidthPx);
        return win;
    }

    /// <summary>配置热键注册: capture/annotate/longshot/floatball (冲突 → 日志+替代建议)。</summary>
    public void RegisterConfiguredHotkeys()
    {
        var cfg = config.Current;
        hotkeys.Register("capture", cfg.Capture.Hotkey);
        hotkeys.Register("annotate", cfg.Capture.AnnotateHotkey);
        hotkeys.Register("longshot", cfg.Capture.LongshotHotkey);
        hotkeys.Register("floatball", cfg.FloatBall.Hotkey);
        hotkeys.Register("ime", "Ctrl+Shift+I");
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

    /// <summary>Wave C: 设置窗口; Wave B 先占位。</summary>
    public void OpenSettings()
    {
        log.LogInformation("[Window] OpenSettings (Wave C 落地)");
    }
}
