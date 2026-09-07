// Windows/Devices/UsbDeviceWatcher.cs —— §6.6: 直接读取系统事件。
// WM_DEVICECHANGE 广播 (VOLUME 到达/移除 + DEVNODES_CHANGED) → 驱动器快照差异扫描。
// 勘误: DBT_DEVTYP_VOLUME 不能经 RegisterDeviceNotification 过滤注册 (err=13);
// 卷事件本就广播给所有顶层窗口, 差异扫描兜底 NODES_CHANGED-only 的设备 (读卡器等)。
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
    private const uint WM_DEVICECHANGE = 0x0219;
    private const uint DBT_DEVICEARRIVAL = 0x8000;
    private const uint DBT_DEVICEREMOVECOMPLETE = 0x8004;
    private const uint DBT_DEVNODES_CHANGED = 0x0007;

    private readonly object _gate = new();
    private nint _hwnd;
    private WndProcCallback? _callback;
    private Dictionary<string, UsbDriveInfo> _lastDrives = [];

    public void Start(nint hostHwnd)
    {
        if (_hwnd != 0) return;
        _hwnd = hostHwnd;
        _lastDrives = Snapshot();
        _callback = OnDeviceChange;
        hook.Add(hostHwnd, _callback);
        log?.LogInformation("[USB] 已监听 WM_DEVICECHANGE (系统广播 + 差异扫描, 基线 {Count} 个可移动盘)", _lastDrives.Count);
    }

    public void Stop()
    {
        if (_hwnd == 0) return;
        if (_callback is not null) hook.Remove(_hwnd, _callback);
        _hwnd = 0;
        log?.LogInformation("[USB] 已停止监听");
    }

    public IReadOnlyList<UsbDriveInfo> ListRemovable() =>
        Snapshot().Values.ToArray();

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
            log?.LogInformation("[USB] 接入 {Drive} ({Label}, {Gb}GB)", info.Drive, info.Label, info.SizeGb);
        }
        foreach (var info in removed)
        {
            bus.Publish(new UsbRemoved(info));
            log?.LogInformation("[USB] 移除 {Drive}", info.Drive);
        }
    }

    /// <summary>驱动器快照差异 (纯函数, 单测锁定)。</summary>
    internal static (List<UsbDriveInfo> Added, List<UsbDriveInfo> Removed) DiffDrives(
        Dictionary<string, UsbDriveInfo> before, Dictionary<string, UsbDriveInfo> after)
    {
        var added = after.Values.Where(d => !before.ContainsKey(d.Drive)).ToList();
        var removed = before.Values.Where(d => !after.ContainsKey(d.Drive)).ToList();
        return (added, removed);
    }

    private Dictionary<string, UsbDriveInfo> Snapshot()
    {
        if (!config.Current.Usb.Enabled) return [];
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
            log?.LogWarning(ex, "[USB] 驱动器扫描异常");
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
