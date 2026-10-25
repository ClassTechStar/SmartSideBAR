// 侧栏面板所需的运行时依赖集合。
// v1.2 各 Panel 通过 window.sidekick.* IPC 取数据; Avalonia 侧同进程直调, 收敛为一个上下文对象,
// 避免每个 Panel 构造函数塞六七个参数。
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Links;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Audio;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Recording;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia.ViewModels;

public sealed record SidebarServices(
    ConfigService Config,
    AppBarService AppBar,
    IImeService Ime,
    IShellService Shell,
    IUsbWatcher Usb,
    IPrinterMonitor Printer,
    SchedulerService Scheduler,
    IRecorderApi Recorder,
    ISoundService Sound,
    LinksService Links,
    Action<string> Dispatch);
