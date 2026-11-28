// 系统指标服务 (方案 §5.2, W7-8): CPU/内存/磁盘/网速, 零 WMI/零 PowerShell。
// CPU = GetSystemTimes 差分; 网速 = GetIfTable 字节差分; 全部跟随 MetricsCalculator 纯计算。
// 温度卡按方案 "诚实降级" 原则未实现 (MSAcpi_ThermalZoneTemperature 多数机器不支持)。
using System.Runtime.InteropServices;
using SmartSideBAR.Core.Metrics;

namespace SmartSideBAR.Windows.SystemMetrics;

public sealed record SystemMetricsSample(
    double CpuPercent,
    double MemoryPercent,
    long MemoryAvailableMb,
    long NetRxBytesPerSec,
    long NetTxBytesPerSec,
    List<(string Drive, long FreeMb, long TotalMb)> Disks);

public interface ISystemMetricsService
{
    /// <summary>推进一次差分采样 (两次调用间隔 ≥500ms 才有意义)。首次调用只建立基线。</summary>
    SystemMetricsSample? Sample();
}

public sealed class SystemMetricsService : ISystemMetricsService
{
    private ulong _lastIdle;
    private ulong _lastTotal;
    private long _lastRx;
    private long _lastTx;
    private DateTimeOffset _lastNetAt = DateTimeOffset.MinValue;
    private bool _primed;

    public SystemMetricsSample? Sample()
    {
        // ---- CPU: GetSystemTimes 差分
        if (!GetSystemTimes(out var idleTs, out var kernelTs, out var userTs)) return null;
        var idle = (ulong)idleTs.Ticks;
        var total = (ulong)kernelTs.Ticks + (ulong)userTs.Ticks;
        if (!_primed)
        {
            _lastIdle = idle;
            _lastTotal = total;
            _primed = true;
            return null; // 首次只建基线
        }
        var cpu = MetricsCalculator.CpuPercent(_lastIdle, _lastTotal, idle, total);
        _lastIdle = idle;
        _lastTotal = total;

        // ---- 内存: GlobalMemoryStatusEx
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref mem)) return null;
        var memPct = mem.dwMemoryLoad;
        var memFreeMb = (long)(mem.ullAvailPhys / (1024 * 1024));

        // ---- 网速: NetworkInterface 字节差分 (内置、裁剪安全、排除回环)
        var now = DateTimeOffset.Now;
        long rx = 0, tx = 0;
        try
        {
            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;
                var stats = nic.GetIPStatistics();
                rx += stats.BytesReceived;
                tx += stats.BytesSent;
            }
        }
        catch { }
        var dt = _lastNetAt == DateTimeOffset.MinValue ? 1.0 : (now - _lastNetAt).TotalSeconds;
        var rxSpeed = MetricsCalculator.BytesPerSecond(_lastRx, rx, dt);
        var txSpeed = MetricsCalculator.BytesPerSecond(_lastTx, tx, dt);
        _lastRx = rx;
        _lastTx = tx;
        _lastNetAt = now;

        // ---- 磁盘: 固定盘符剩余空间
        var disks = new List<(string, long, long)>();
        foreach (var drive in Environment.GetLogicalDrives())
        {
            try
            {
                if (GetDriveType(drive[..2]) != 3) continue; // DRIVE_FIXED
                if (GetDiskFreeSpaceEx(drive, out var free, out var totalBytes, out _) && totalBytes > 0)
                {
                    disks.Add((drive[..2], (long)(free / (1024 * 1024)), (long)(totalBytes / (1024 * 1024))));
                }
            }
            catch { /* 单盘异常不影响整体 */ }
        }

        return new SystemMetricsSample(cpu, memPct, memFreeMb, rxSpeed, txSpeed, disks);
    }

    // ---- P/Invoke ----
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out TimeSpan lpIdleTime, out TimeSpan lpKernelTime, out TimeSpan lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
        out ulong lpFreeBytesAvailable, out ulong lpTotalNumberOfBytes, out ulong lpTotalNumberOfFreeBytes);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetDriveType(string lpRootPathName);
}
