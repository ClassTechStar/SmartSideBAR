// Wave C 修订: USB 差异扫描纯函数测试 (直接读取系统事件 + 驱动器快照 diff)
using SmartSideBAR.Windows.Devices;
using Xunit;

namespace SmartSideBAR.Windows.Tests;

public sealed class UsbDeviceWatcherTests
{
    private static UsbDriveInfo D(string letter, string label = "U盘") =>
        new($"{letter}:\\", label, 32.0, true);

    [Fact]
    public void DiffDrives_ArrivalAndRemoval()
    {
        var before = new Dictionary<string, UsbDriveInfo> { ["E:\\"] = D("E"), ["F:\\"] = D("F") };
        var after = new Dictionary<string, UsbDriveInfo> { ["F:\\"] = D("F"), ["G:\\"] = D("G") };

        var (added, removed) = UsbDeviceWatcher.DiffDrives(before, after);

        Assert.Single(added);
        Assert.Equal("G:\\", added[0].Drive);
        Assert.Single(removed);
        Assert.Equal("E:\\", removed[0].Drive);
    }

    [Fact]
    public void DiffDrives_NoChange_NoEvents()
    {
        var drives = new Dictionary<string, UsbDriveInfo> { ["E:\\"] = D("E") };
        var (added, removed) = UsbDeviceWatcher.DiffDrives(drives, drives);
        Assert.Empty(added);
        Assert.Empty(removed);
    }

    [Fact]
    public void DiffDrives_LabelRefresh_IsNotAnEvent()
    {
        // 同盘符仅卷标变化不算插拔
        var before = new Dictionary<string, UsbDriveInfo> { ["E:\\"] = D("E", "旧卷标") };
        var after = new Dictionary<string, UsbDriveInfo> { ["E:\\"] = D("E", "新卷标") };
        var (added, removed) = UsbDeviceWatcher.DiffDrives(before, after);
        Assert.Empty(added);
        Assert.Empty(removed);
    }
}
