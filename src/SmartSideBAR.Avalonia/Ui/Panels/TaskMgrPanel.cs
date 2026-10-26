// TaskMgrPanel, v1.2 同名面板的 Avalonia 实现。
// P2-1：由 697 行的 SidebarPanels.cs 拆出，一面板一文件（单文件 ≤600 行的拆分约定），
// 便于单独单测、减少合并冲突。公共构件见 PanelKit.cs，基类见 SidebarPanel.cs。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Devices;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.Devices;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed class TaskMgrPanel(SidebarServices s, Palette p) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var root = Stack(12);
        root.Children.Add(PanelKit.Hint("课堂投屏卡顿或应用无响应时，可在这里一键打开系统任务管理器结束占用。", Palette));
        root.Children.Add(PanelKit.Flat("打开任务管理器", Palette, () => Services.Dispatch("taskmgr"), primary: true));
        return root;
    }
}
