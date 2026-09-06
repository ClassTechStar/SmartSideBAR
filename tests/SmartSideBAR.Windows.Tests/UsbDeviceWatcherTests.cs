// Wave B Windows 层测试: USB 盘符解析 (零 WMI 路径的纯函数)
using SmartSideBAR.Windows.Devices;
using Xunit;

namespace SmartSideBAR.Windows.Tests;

public sealed class UsbDeviceWatcherTests
{
    [Fact]
    public void UnitMaskToLetter_SingleBit()
    {
        Assert.Equal("E:\\", UsbDeviceWatcher.UnitMaskToLetter(1u << 4));
        Assert.Equal("A:\\", UsbDeviceWatcher.UnitMaskToLetter(1u));
        Assert.Equal("Z:\\", UsbDeviceWatcher.UnitMaskToLetter(1u << 25));
    }

    [Fact]
    public void UnitMaskToLetter_FirstBitWins()
    {
        Assert.Equal("D:\\", UsbDeviceWatcher.UnitMaskToLetter((1u << 3) | (1u << 5)));
        Assert.Null(UsbDeviceWatcher.UnitMaskToLetter(0));
    }
}
