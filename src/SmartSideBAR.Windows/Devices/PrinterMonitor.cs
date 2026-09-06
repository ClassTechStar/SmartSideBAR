// Windows/Devices/PrinterMonitor.cs —— §6.7: System.Management 同源 WMI, E4 状态映射根修。
// 轮询间隔消费 config.printer.pollIntervalSec; 状态变化 → bus.Publish(PrinterChanged)。
using System.Management;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Devices;
using SmartSideBAR.Core.Messaging;

namespace SmartSideBAR.Windows.Devices;

public sealed record PrinterInfo(string Name, PrinterState State, string WireState);

public sealed record PrinterChanged(IReadOnlyList<PrinterInfo> Printers);

public interface IPrinterMonitor
{
    void Start();
    void Stop();
    IReadOnlyList<PrinterInfo> Query();
}

public sealed class PrinterMonitor(
    ConfigService config,
    IEventBus bus,
    ILogger<PrinterMonitor>? log = null) : IPrinterMonitor, IDisposable
{
    private System.Threading.Timer? _timer;
    private string _lastHash = "";
    private readonly object _gate = new();

    public void Start()
    {
        lock (_gate)
        {
            if (_timer is not null) return;
            var interval = TimeSpan.FromSeconds(Math.Max(3, config.Current.Printer.PollIntervalSec));
            _timer = new System.Threading.Timer(_ => PollOnce(), null, TimeSpan.Zero, interval);
            log?.LogInformation("[Printer] 已启动轮询 ({IntervalSec}s)", interval.TotalSeconds);
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            log?.LogInformation("[Printer] 已停止轮询");
        }
    }

    public IReadOnlyList<PrinterInfo> Query()
    {
        var list = new List<PrinterInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, PrinterStatus, DetectedErrorState FROM Win32_Printer");
            foreach (var mo in searcher.Get())
            {
                var name = mo["Name"] as string ?? "(未知)";
                uint? detected = mo["DetectedErrorState"] is null ? null : Convert.ToUInt32(mo["DetectedErrorState"]);
                ushort? status = mo["PrinterStatus"] is null ? null : Convert.ToUInt16(mo["PrinterStatus"]);
                var state = PrinterStateMapper.Map(detected, status);
                list.Add(new PrinterInfo(name, state, PrinterStateMapper.ToWire(state)));
            }
        }
        catch (Exception ex)
        {
            // WMI 服务异常 (被禁用/权限) 不阻断常驻; 下轮重试
            log?.LogWarning(ex, "[Printer] WMI 查询失败");
        }
        return list;
    }

    private void PollOnce()
    {
        try
        {
            var printers = Query();
            var hash = string.Join("|", printers.Select(p => $"{p.Name}:{p.WireState}"));
            if (hash == _lastHash) return; // 变更检测: 无变化不广播
            _lastHash = hash;
            bus.Publish(new PrinterChanged(printers));
            log?.LogInformation("[Printer] 状态变化: {Count} 台", printers.Count);
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "[Printer] 轮询异常");
        }
    }

    public void Dispose() => Stop();
}
