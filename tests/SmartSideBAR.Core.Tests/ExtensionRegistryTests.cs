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

    [Fact]
    public void 共享注册中心可登记与注销()
    {
        var reg = ExtensionHost.Registry;
        var id = "test.ext." + Guid.NewGuid().ToString("N")[..6];
        try
        {
            Assert.True(reg.TryRegister(new ExtensionManifest(id, "测试扩展", ExtensionRegistry.CurrentApiVersion, "0.1")));
            Assert.Contains(reg.Loaded, e => e.Id == id);
        }
        finally
        {
            reg.Unregister(id); // 共享单例, 测后清理避免跨测试污染
        }
    }

    [Fact]
    public void 失败记录显示名与挂件端点()
    {
        Assert.Equal("(未命名)", new ExtensionLoadFailure("", "原因").DisplayName);
        Assert.Equal("demo", new ExtensionLoadFailure("demo", "原因").DisplayName);
        var ep = new ExtensionEntryPoint("demo", "panel", () => new object());
        Assert.Equal("demo", ep.ExtensionId);
        Assert.Equal("panel", ep.Kind);
        Assert.NotNull(ep.Factory());
    }
}
