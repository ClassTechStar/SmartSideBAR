// 版本比较与安装包匹配 (P2-4)。
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Core.Tests;

public class VersionComparerTests
{
    [Theory]
    [InlineData("1.5.0", "1.3.0", 1)]
    [InlineData("1.3.0", "1.5.0", -1)]
    [InlineData("1.5.0", "1.5.0", 0)]
    [InlineData("v1.5.0", "1.5.0", 0)]
    public void 版本比较(string a, string b, int expected)
        => Assert.Equal(expected, VersionComparer.Compare(a, b));

    [Fact]
    public void 段数不同时补零比较()
    {
        Assert.Equal(0, VersionComparer.Compare("1.5", "1.5.0"));
        Assert.True(VersionComparer.IsNewer("1.5.1", "1.5"));
    }

    [Fact]
    public void 带后缀的版本只取前导数字()
    {
        Assert.Equal(0, VersionComparer.Compare("1.5.0-beta", "1.5.0"));
        Assert.True(VersionComparer.IsNewer("1.6.0-beta", "1.5.0"));
    }

    [Fact]
    public void 空值与非法值按0处理()
    {
        Assert.Equal(0, VersionComparer.Compare(null, ""));
        Assert.True(VersionComparer.IsNewer("0.0.1", null));
    }

    [Fact]
    public void 安装包名匹配()
    {
        Assert.True(VersionComparer.IsInstaller("SmartSideBAR-v1.5.0-x64-Setup.exe"));
        Assert.False(VersionComparer.IsInstaller("SmartSideBAR-v1.5.0-x64.zip"));
        Assert.False(VersionComparer.IsInstaller(""));
        Assert.False(VersionComparer.IsInstaller(null));
    }
}
