// 窗口集中管理 (方案 §4.1): 侧栏 AppBar 轨窗口的创建与挂接。
// 坐标 (K4): Avalonia 侧不自行定位 —— 窗口位置由 AppBarService 经 MoveWindow 以物理像素下达。
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Avalonia.Views;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Links;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.AppBar;

namespace SmartSideBAR.Avalonia;

public sealed class WindowManager(
    ConfigService config,
    LinksService links,
    AppBarService appBar,
    IEventBus bus,
    ILogger<WindowManager> log)
{
    /// <summary>rail 收起宽度 (物理像素) —— 方案 §5.3 默认</summary>
    public const int RailWidthPx = 64;
    /// <summary>面板展开宽度 (物理像素)</summary>
    public const int ExpandedWidthPx = 420;

    public SidebarWindow CreateSidebar()
    {
        var cfg = config.Current;
        var side = cfg.Display.SidebarSide == SidebarSide.Left ? AppBarEdge.Left : AppBarEdge.Right;

        var win = new SidebarWindow(side)
        {
            DataContext = new ViewModels.SidebarViewModel(appBar, links.List()),
        };
        win.Opened += (_, _) =>
        {
            var handle = win.TryGetPlatformHandle();
            if (handle is null)
            {
                log.LogWarning("[Window] 未能获取平台句柄, 侧栏以 Topmost 模式运行");
                win.Topmost = true;
                return;
            }

            // §5.4: AppBar 模式下不置顶 (系统保证不被最大化遮盖); 注册失败才回退 alwaysOnTop
            if (appBar.Attach(handle.Handle, side, RailWidthPx, ExpandedWidthPx))
            {
                win.Topmost = false;
            }
            else
            {
                win.Topmost = true; // v1.2.0 同款 fallback
                log.LogWarning("[Window] AppBar 注册失败 → Topmost 回退");
            }
        };
        win.Closed += (_, _) => appBar.Detach();

        // 几何事件: Wave A 仅记录; Wave B 用于面板内容宽度同步 (不自算, 以系统授予为准)
        bus.Subscribe<AppBarGeometryChanged>(g =>
            log.LogDebug("[Window] AppBar 几何: ({X},{Y}) {W}x{H}px", g.Rect.X, g.Rect.Y, g.Rect.W, g.Rect.H));

        log.LogInformation("[Window] 侧栏窗口已创建 ({Side} 侧, rail={Rail}px)", side, RailWidthPx);
        return win;
    }
}
