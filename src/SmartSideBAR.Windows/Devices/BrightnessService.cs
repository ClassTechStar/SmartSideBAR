// 屏幕亮度调节 (P1-4 的一部分)。
// 借鉴 DiffiTouch 的 brightness_page：课堂场景常用。
// 通过 WMI 的 WmiMonitorBrightness（root/WMI）读写；台式机/无背光接口的设备不支持，
// 由 IsSupported 提前探测，调用方据此隐藏滑杆，而不是摆一个点了没反应的控件。
using System.Management;

namespace SmartSideBAR.Windows.Devices;

public sealed class BrightnessService
{
    private bool? _supported;

    /// <summary>是否支持亮度调节（探测结果会缓存）。</summary>
    public bool IsSupported
    {
        get
        {
            _supported ??= Probe();
            return _supported.Value;
        }
    }

    private static bool Probe()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            using var results = searcher.Get();
            foreach (ManagementObject _ in results) return true;
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>读取当前亮度百分比；不支持或失败返回 null。</summary>
    public int? Get()
    {
        if (!IsSupported) return null;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI", "SELECT CurrentBrightness FROM WmiMonitorBrightness");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                var v = obj["CurrentBrightness"];
                if (v is byte or int) return Clamp(Convert.ToInt32(v));
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>设置亮度百分比（0–100）。不支持或失败返回 false。</summary>
    public bool Set(int percent)
    {
        if (!IsSupported) return false;
        var value = (byte)Clamp(percent);
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "root\\WMI", "SELECT InstanceName FROM WmiMonitorBrightnessMethods");
            using var results = searcher.Get();
            foreach (ManagementObject obj in results)
            {
                var instanceName = obj["InstanceName"]?.ToString();
                if (string.IsNullOrEmpty(instanceName)) continue;
                using var classInstance = new ManagementClass("root\\WMI", "WmiMonitorBrightnessMethods", null);
                using var inParams = classInstance.GetMethodParameters("WmiSetBrightness");
                inParams["Brightness"] = value;
                inParams["Timeout"] = 1u;
                // 用带 InstanceName 的实例调用
                using var instance = new ManagementObject(
                    "root\\WMI", $"WmiMonitorBrightnessMethods.InstanceName='{instanceName.Replace("'", "\\'")}'", null);
                _ = instance.InvokeMethod("WmiSetBrightness", inParams, null);
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static int Clamp(int v) => Math.Clamp(v, 0, 100);
}
