// 面板注册表 (P2-1)。
using SmartSideBAR.Core.Panels;

namespace SmartSideBAR.Core.Tests;

public class PanelRegistryTests
{
    private static readonly PanelDefinition Capture = new("capture", "截图", "fluent:camera", Order: 1);
    private static readonly PanelDefinition Usb = new("usb", "U 盘", Order: 2);
    private static readonly PanelDefinition Links = new("links", "链接", Order: 3);

    [Fact]
    public void 注册后可按id取回()
    {
        var reg = new PanelRegistry();
        Assert.True(reg.Register(Capture));
        Assert.Equal("截图", reg.Get("capture")!.Label);
    }

    [Fact]
    public void 空id被拒()
    {
        var reg = new PanelRegistry();
        Assert.False(reg.Register(new PanelDefinition("", "")));
    }

    [Fact]
    public void 按Order排序()
    {
        var reg = new PanelRegistry();
        reg.Register(Links);
        reg.Register(Capture);
        reg.Register(Usb);
        Assert.Equal(new[] { "capture", "usb", "links" }, reg.All.Select(p => p.Id));
    }

    [Fact]
    public void 禁用名单过滤生效()
    {
        var reg = new PanelRegistry();
        reg.Register(Capture);
        reg.Register(Usb);
        reg.Register(Links);
        var visible = reg.Visible(["usb"]);
        Assert.Equal(new[] { "capture", "links" }, visible.Select(p => p.Id));
    }

    [Fact]
    public void 定义本身禁用时不出现在可见列表()
    {
        var reg = new PanelRegistry();
        reg.Register(Capture with { Enabled = false });
        Assert.Empty(reg.Visible(null));
        Assert.Single(reg.All);
    }

    [Fact]
    public void 可注销与复位()
    {
        var reg = new PanelRegistry();
        reg.Register(Capture);
        Assert.True(reg.Unregister("capture"));
        Assert.Empty(reg.All);
    }
}
