// M3: 任务管理面板 VM（原 TaskMgrPanel 的代码构建逻辑迁到此处）。
using SmartSideBAR.Avalonia.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Panels.ViewModels;

internal sealed class TaskMgrPanelViewModel(SidebarServices services, Palette palette)
    : PanelViewModel(services, palette)
{
    public string Hint { get; } = "投屏卡顿或程序无响应时，从这里打开系统任务管理器。";

    public RelayCommand OpenTaskManagerCommand { get; } = new(_ => services.Dispatch("taskmgr"));

    public override void Reload()
    {
        // 静态内容, 无需刷新
    }
}
