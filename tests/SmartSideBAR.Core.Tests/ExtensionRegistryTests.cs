// 轻量扩展点 (P2-1): API 版本校验与优雅失败。
using SmartSideBAR.Core.Extensions;

namespace SmartSideBAR.Core.Tests;

public class ExtensionRegistryTests
{
    [Fact]
    public void 版本匹配可注册()
    {
        var reg = new ExtensionRegistry();
        var ok = reg.TryRegister(new ExtensionManifest("demo", "示例", ExtensionRegistry.CurrentApiVersion));
        Assert.True(ok);
        Assert.Single(reg.Loaded);
    }

    [Fact]
    public void 版本不匹配被拒并记录失败原因()
    {
        var reg = new ExtensionRegistry();
        Assert.False(reg.TryRegister(new ExtensionManifest("old", "旧扩展", 999)));
        Assert.Empty(reg.Loaded);
        Assert.Single(reg.Failures);
        Assert.Contains("API 版本不匹配", reg.Failures[0].Reason);
    }

    [Fact]
    public void 空Id被拒()
    {
        var reg = new ExtensionRegistry();
        Assert.False(reg.TryRegister(new ExtensionManifest("", "", ExtensionRegistry.CurrentApiVersion)));
        Assert.Single(reg.Failures);
    }

    [Fact]
    public void 重复注册被拒()
    {
        var reg = new ExtensionRegistry();
        reg.TryRegister(new ExtensionManifest("a", "A", ExtensionRegistry.CurrentApiVersion));
        Assert.False(reg.TryRegister(new ExtensionManifest("a", "A", ExtensionRegistry.CurrentApiVersion)));
        Assert.Single(reg.Loaded);
    }

    [Fact]
    public void 可注销并复位()
    {
        var reg = new ExtensionRegistry();
        reg.TryRegister(new ExtensionManifest("a", "A", ExtensionRegistry.CurrentApiVersion));
        Assert.True(reg.Unregister("a"));
        Assert.Empty(reg.Loaded);
    }
}
