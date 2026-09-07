// Windows/Devices/UsbDeviceWatcher.cs —— §6.6: 直接读取系统事件。
// 三层检测 (对齐 v1.1 usb.ts):
//   1. WMI Win32_VolumeChangeEvent 事件驱动 (ManagementEventWatcher)
//   2. WM_DEVICECHANGE 广播 (所有顶层窗口) → 差异扫描
//   3. WMI CIM 综合扫描 (USB 物理磁盘 → 分区 → 逻辑磁盘, 覆盖 USB 3.0 SATA 桥接)
// 声音提醒: 双音 SystemSounds.Asterisk (v1.1 playUsbSound)
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.Native;
using System.Management;

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
    private const uint WM_DEVICECHANGE = 0x0219;
    private const uint DBT_DEVICEARRIVAL = 0x8000;
    private const uint DBT_DEVICEREMOVECOMPLETE = 0x8004;
    private const uint DBT_DEVNODES_CHANGED = 0x0007;

    private readonly object _gate = new();
    private nint _hwnd;
    private WndProcCallback? _callback;
    private Dictionary<string, UsbDriveInfo> _lastDrives = [];
    private ManagementEventWatcher? _wmiWatcher;
    private bool _running;

    public void Start(nint hostHwnd)
    {
        if (_hwnd != 0) return;
        _hwnd = hostHwnd;
        _running = true;
        _lastDrives = Snapshot();
        _callback = OnDeviceChange;
        hook.Add(hostHwnd, _callback);
        StartWmiWatcher();
        log?.LogInformation("[USB] 已监听 WM_DEVICECHANGE + WMI VolumeChangeEvent (基线 {Count} 个可移动盘)", _lastDrives.Count);
    }

    public void Stop()
    {
        _running = false;
        if (_callback is not null) hook.Remove(_hwnd, _callback);
        _hwnd = 0;
        StopWmiWatcher();
        log?.LogInformation("[USB] 已停止监听");
    }

    public IReadOnlyList<UsbDriveInfo> ListRemovable() =>
        Snapshot().Values.ToArray();

    // ---- WMI 事件监听 (v1.1 主路径: Win32_VolumeChangeEvent) ----

    private void StartWmiWatcher()
    {
        try
        {
            var query = new WqlEventQuery("SELECT * FROM Win32_VolumeChangeEvent");
            _wmiWatcher = new ManagementEventWatcher(query);
            _wmiWatcher.EventArrived += (_, args) =>
            {
                if (!_running) return;
                var driveName = args.NewEvent["DriveName"]?.ToString() ?? "unknown";
                var eventType = args.NewEvent["EventType"]?.ToString() ?? "0";
                log?.LogInformation("[USB] WMI 事件: type={Type}, drive={Drive}", eventType, driveName);
                // EventType: 2 = arrival, 3 = removal
                var delay = eventType == "2" ? 300 : 500;
                _ = Task.Run(async () =>
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                    RescanWithDiff();
                });
            };
            _wmiWatcher.Start();
            log?.LogInformation("[USB] WMI VolumeChangeEvent 监听已启动");
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "[USB] WMI 事件监听启动失败, 依赖 WM_DEVICECHANGE 广播");
        }
    }

    private void StopWmiWatcher()
    {
        if (_wmiWatcher is null) return;
        try
        {
            _wmiWatcher.Stop();
            _wmiWatcher.Dispose();
        }
        catch { /* 尽力而为 */ }
        _wmiWatcher = null;
    }

    private nint OnDeviceChange(nint hwnd, uint msg, nuint wParam, nint lParam, ref bool handled)
    {
        if (msg != WM_DEVICECHANGE) return 0;
        handled = true;
        // 到达/移除/设备节点变化 三类都值得重扫; 卷挂载后文件系统可能未就绪, 延迟再扫一次
        RescanWithDiff();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(900).ConfigureAwait(false);
                RescanWithDiff();
            }
            catch { /* 尽力而为 */ }
        });
        return 0;
    }

    private void RescanWithDiff()
    {
        List<UsbDriveInfo> added;
        List<UsbDriveInfo> removed;
        lock (_gate)
        {
            var now = Snapshot();
            (added, removed) = DiffDrives(_lastDrives, now);
            _lastDrives = now;
        }
        foreach (var info in added)
        {
            bus.Publish(new UsbArrived(info));
            PlayUsbSound();
            log?.LogInformation("[USB] 接入 {Drive} ({Label}, {Gb}GB)", info.Drive, info.Label, info.SizeGb);
        }
        foreach (var info in removed)
        {
            bus.Publish(new UsbRemoved(info));
            log?.LogInformation("[USB] 移除 {Drive}", info.Drive);
        }
    }

    // ---- v1.1 双音提示音 (SystemAsterisk x2, winmm 直调) ----

    private static void PlayUsbSound()
    {
        _ = Task.Run(() =>
        {
            try
            {
                Winmm.PlaySound("SystemAsterisk", 0, Winmm.SND_ALIAS | Winmm.SND_ASYNC);
                Thread.Sleep(240);
                Winmm.PlaySound("SystemAsterisk", 0, Winmm.SND_ALIAS | Winmm.SND_ASYNC);
            }
            catch { /* 尽力而为 */ }
        });
    }

    /// <summary>驱动器快照差异 (纯函数, 单测锁定)。</summary>
    internal static (List<UsbDriveInfo> Added, List<UsbDriveInfo> Removed) DiffDrives(
        Dictionary<string, UsbDriveInfo> before, Dictionary<string, UsbDriveInfo> after)
    {
        var added = after.Values.Where(d => !before.ContainsKey(d.Drive)).ToList();
        var removed = before.Values.Where(d => !after.ContainsKey(d.Drive)).ToList();
        return (added, removed);
    }

    /// <summary>综合快照: 优先 WMI CIM 关联查询, 失败回退 DriveInfo。</summary>
    private Dictionary<string, UsbDriveInfo> Snapshot()
    {
        if (!config.Current.Usb.Enabled) return [];

        // 第 1 层: WMI CIM 综合扫描 (覆盖 USB 3.0 SATA 桥接移动硬盘)
        var wmiResult = SnapshotWmi();
        if (wmiResult.Count > 0) return wmiResult;

        // 第 2 层: 回退 DriveInfo (WMI 不可用时)
        return SnapshotDriveInfo();
    }

    /// <summary>WMI CIM 关联查询: USB 物理磁盘 → 分区 → 逻辑磁盘。</summary>
    private Dictionary<string, UsbDriveInfo> SnapshotWmi()
    {
        var result = new Dictionary<string, UsbDriveInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Part A: DriveType=2(Removable) or 5(CDRom) 直接检测
            using var diskSearcher = new ManagementObjectSearcher(
                "SELECT DeviceID, VolumeName, DriveType, Size FROM Win32_LogicalDisk WHERE DriveType=2 OR DriveType=5");
            foreach (var obj in diskSearcher.Get().Cast<ManagementObject>())
            {
                using (obj)
                {
                    var drive = obj["DeviceID"]?.ToString();
                    if (string.IsNullOrEmpty(drive)) continue;
                    var label = obj["VolumeName"]?.ToString();
                    var size = obj["Size"] != null ? Convert.ToDouble(obj["Size"]) : 0.0;
                    var gb = size > 0 ? Math.Round(size / 1073741824.0, 1) : 0.0;
                    result[drive] = new UsbDriveInfo(drive,
                        string.IsNullOrWhiteSpace(label) ? null : label, gb, true);
                }
            }

            // Part B: USB 物理磁盘 CIM 关联 (覆盖 USB 3.0 SATA 桥接芯片, DriveType=Fixed 的移动硬盘)
            using var usbSearcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB' OR PNPDeviceID LIKE '%USB%' OR PNPDeviceID LIKE '%USBSTOR%' OR MediaType='Removable media' OR MediaType='External hard disk media'");
            foreach (var disk in usbSearcher.Get().Cast<ManagementObject>())
            {
                using (disk)
                {
                    // CIM 关联: DiskDrive → DiskPartition → LogicalDisk
                    foreach (var partition in disk.GetRelated("Win32_DiskDriveToDiskPartition").Cast<ManagementObject>())
                    {
                        using (partition)
                        {
                            foreach (var ld in partition.GetRelated("Win32_LogicalDiskToPartition").Cast<ManagementObject>())
                            {
                                using (ld)
                                {
                                    var did = ld["DeviceID"]?.ToString();
                                    if (string.IsNullOrEmpty(did) || result.ContainsKey(did)) continue;
                                    var label = ld["VolumeName"]?.ToString();
                                    var size = ld["Size"] != null ? Convert.ToDouble(ld["Size"]) : 0.0;
                                    var gb = size > 0 ? Math.Round(size / 1073741824.0, 1) : 0.0;
                                    result[did] = new UsbDriveInfo(did,
                                        string.IsNullOrWhiteSpace(label) ? null : label, gb, false);
                                }
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "[USB] WMI 扫描异常, 回退 DriveInfo");
        }
        return result;
    }

    /// <summary>DriveInfo 回退扫描 (WMI 不可用时)。</summary>
    private Dictionary<string, UsbDriveInfo> SnapshotDriveInfo()
    {
        var result = new Dictionary<string, UsbDriveInfo>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var d in DriveInfo.GetDrives())
            {
                if (d.DriveType is not (DriveType.Removable or DriveType.CDRom)) continue;
                result[d.Name] = ToInfo(d);
            }
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "[USB] DriveInfo 扫描异常");
        }
        return result;
    }

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

    public void Dispose() => Stop();
}
