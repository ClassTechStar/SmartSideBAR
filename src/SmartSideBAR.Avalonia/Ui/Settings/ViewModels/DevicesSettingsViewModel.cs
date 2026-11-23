// 设备页 VM: U 盘监控开关与打印机轮询间隔。
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

public sealed class DevicesSettingsViewModel : SettingsPageViewModel
{
    private bool _usbEnabled = true;
    private double _printerIntervalSec = 10;

    public override string Title => "设备";

    public bool UsbEnabled { get => _usbEnabled; set => SetProperty(ref _usbEnabled, value); }

    public double PrinterIntervalSec { get => _printerIntervalSec; set => SetProperty(ref _printerIntervalSec, value); }

    public override void LoadFrom(SidekickConfig config)
    {
        UsbEnabled = config.Usb.Enabled;
        PrinterIntervalSec = config.Printer.PollIntervalSec;
    }

    public override IEnumerable<ConfigChange> CollectChanges() =>
    [
        ConfigChange.From("usb.enabled", UsbEnabled),
        ConfigChange.From("printer.pollIntervalSec", (int)Math.Clamp(PrinterIntervalSec, 3, 300)),
    ];
}
