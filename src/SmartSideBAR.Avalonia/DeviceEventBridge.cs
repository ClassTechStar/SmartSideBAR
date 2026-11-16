// 设备/服务事件 → 轻反馈 (P1-9 从 WindowManager.CreateSidebar 的闭包订阅拆出):
// 专注「事件 → Toast/统计」, 窗口生命周期留在 WindowManager, 动作路由见 WindowRouter。
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Core.Telemetry;
using SmartSideBAR.Windows.Capture;
using SmartSideBAR.Windows.Devices;
using SmartSideBAR.Windows.Recording;

namespace SmartSideBAR.Avalonia;

internal sealed class DeviceEventBridge(IEventBus bus, LessonStatsService stats) : IDisposable
{
    private readonly List<IDisposable> _subs = [];

    public void Attach()
    {
        _subs.Add(bus.Subscribe<UsbArrived>(e =>
        {
            stats.Record("usb");
            ToastService.Instance.Show($"U 盘已接入: {e.Info.Drive} {e.Info.Label ?? ""} {e.Info.SizeGb}GB", "info");
        }));
        _subs.Add(bus.Subscribe<UsbRemoved>(e => ToastService.Instance.Show($"U 盘已移除: {e.Info.Drive}", "warn")));
        _subs.Add(bus.Subscribe<PrinterChanged>(e =>
        {
            var abnormal = e.Printers.Where(p => p.WireState is not "ok");
            foreach (var p in abnormal)
            {
                ToastService.Instance.Show($"打印机「{p.Name}」状态: {p.WireState}", "warn");
            }
        }));
        _subs.Add(bus.Subscribe<RecorderStatusChanged>(e =>
        {
            if (e.Recording) stats.Record("record");
            ToastService.Instance.Show(e.Recording ? $"开始录屏: {e.FilePath}" : $"录屏完成: {e.FilePath}", e.Recording ? "info" : "success");
        }));
        _subs.Add(bus.Subscribe<LongshotCompleted>(e =>
        {
            if (e.Error is null) stats.Record("longshot");
            ToastService.Instance.Show(e.Error is null ? $"长截图完成: {e.FilePath}" : $"长截图失败: {e.Error}", e.Error is null ? "success" : "error");
        }));

        // B7: 到点必须有可见反馈, 只播声音不够 (教师戴耳麦时听不见)
        _subs.Add(bus.Subscribe<ReminderDue>(e =>
        {
            stats.Record("reminder");
            ToastService.Instance.Show($"提醒: {e.Note ?? "时间到了"}", "warn");
        }));
    }

    public void Dispose()
    {
        foreach (var s in _subs) s.Dispose();
        _subs.Clear();
    }
}
