// Windows/Devices/UsbDeviceWatcher.cs —— §6.6 (ADR-M5): WM_DEVICECHANGE 消息驱动, 零 PowerShell/零 WMI。
// v1.2.0 的 WMI 事件+巡检+PS 降级三层补丁整体退役; 延迟 ≤1s (原 ≤2s 承诺)。
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Windows.Devices;

public sealed record UsbDriveInfo(string Drive, string? Label, double SizeGb, bool Removable);

public sealed record UsbArrived(UsbDriveInfo Info);
public sealed record UsbRemoved(UsbDriveInfo Info);

public interface IUsbWatcher
{
    void Start(nint hostHwnd);
    void Stop();
    IReadOnlyList<UsbDriveInfo> ListRemovable();
}

public sealed class UsbDeviceWatcher(
    IWndProcHook hook,
    IEventBus bus,
    ConfigService config,
    ILogger<UsbDeviceWatcher>? log = null) : IUsbWatcher, IDisposable
{
    private nint _hwnd;
    private nint _notificationHandle;
    private WndProcCallback? _callback;

    public void Start(nint hostHwnd)
    {
        if (_hwnd != 0) return;
        _hwnd = hostHwnd;
        var volume = new Win32Input.DEV_BROADCAST_VOLUME
        {
            dbcv_size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Win32Input.DEV_BROADCAST_VOLUME>(),
            dbcv_devicetype = Win32Input.DBT_DEVTYP_VOLUME,
        };
        _notificationHandle = Win32Input.RegisterDeviceNotification(hostHwnd, ref volume, Win32Input.DEVICE_NOTIFY_WINDOW_HANDLE);
        _callback = OnDeviceChange;
        hook.Add(hostHwnd, _callback);
        log?.LogInformation("[USB] 已监听 WM_DEVICECHANGE (notification={Ok})", _notificationHandle != 0);
    }

    public void Stop()
    {
        if (_hwnd == 0) return;
        if (_callback is not null) hook.Remove(_hwnd, _callback);
        if (_notificationHandle != 0) _ = Win32Input.UnregisterDeviceNotification(_notificationHandle);
        _hwnd = 0;
        _notificationHandle = 0;
        log?.LogInformation("[USB] 已停止监听");
    }

    public IReadOnlyList<UsbDriveInfo> ListRemovable() =>
        [.. DriveInfo.GetDrives().Where(IsTargetDrive).Select(ToInfo)];

    private nint OnDeviceChange(nint hwnd, uint msg, nuint wParam, nint lParam, ref bool handled)
    {
        if (msg != Win32Input.WM_DEVICECHANGE) return 0;
        if (wParam is not (Win32Input.DBT_DEVICEARRIVAL or Win32Input.DBT_DEVICEREMOVECOMPLETE)) return 0;
        if (lParam == 0) return 0;

        var volume = System.Runtime.InteropServices.Marshal.PtrToStructure<Win32Input.DEV_BROADCAST_VOLUME>(lParam);
        if (volume.dbcv_devicetype != Win32Input.DBT_DEVTYP_VOLUME) return 0;

        var letter = UnitMaskToLetter(volume.dbcv_unitmask);
        if (letter is null) return 0;
        if (!IsAllowedDrive(letter)) { log?.LogDebug("[USB] 忽略非目标盘 {Drive}:", letter); return 0; }

        var info = ToInfo(new DriveInfo(letter));
        handled = true;
        if (wParam == Win32Input.DBT_DEVICEARRIVAL)
        {
            bus.Publish(new UsbArrived(info));
            log?.LogInformation("[USB] 接入 {Drive} ({Label})", info.Drive, info.Label);
        }
        else
        {
            bus.Publish(new UsbRemoved(info));
            log?.LogInformation("[USB] 移除 {Drive}", info.Drive);
        }
        return 0;
    }

    /// <summary>config.usb.ignoreTypes 语义保留: 过滤由调用方/策略完成; 此处仅留扩展点。</summary>
    private bool IsAllowedDrive(string letter) => config.Current.Usb.Enabled;

    private static bool IsTargetDrive(DriveInfo d) =>
        d.DriveType == DriveType.Removable || d.DriveType == DriveType.CDRom;

    private static UsbDriveInfo ToInfo(DriveInfo d)
    {
        double gb = 0;
        string? label = null;
        try
        {
            if (d.IsReady)
            {
                gb = Math.Round(d.TotalSize / 1073741824.0, 1);
                label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? null : d.VolumeLabel;
            }
        }
        catch { /* 移除竞态: 元数据尽力而为 */ }
        return new UsbDriveInfo(d.Name, label, gb, d.DriveType == DriveType.Removable);
    }

    internal static string? UnitMaskToLetter(uint unitMask)
    {
        for (var i = 0; i < 26; i++)
        {
            if ((unitMask & (1u << i)) != 0) return $"{(char)('A' + i)}:\\";
        }
        return null;
    }

    public void Dispose() => Stop();
}
