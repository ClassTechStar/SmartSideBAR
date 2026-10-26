// 配置键级订阅与锁定键 (P1-4)。
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Tests;

public class ConfigSubscriptionTests
{
    [Fact]
    public void 精确键订阅只在匹配时触发()
    {
        var sub = new ConfigSubscription();
        var hit = 0;
        sub.Subscribe("display.railTrigger", () => hit++);

        Assert.Equal(0, sub.Notify("display.sidebarSide"));
        Assert.Equal(1, sub.Notify("display.railTrigger"));
        Assert.Equal(1, hit);
    }

    [Fact]
    public void 前缀订阅覆盖该前缀下所有键()
    {
        var sub = new ConfigSubscription();
        var hit = 0;
        sub.SubscribePrefix("display.", () => hit++);

        sub.Notify("display.sidebarSide");
        sub.Notify("display.railTrigger");
        Assert.Equal(2, hit);
        Assert.Equal(0, sub.Notify("appearance.accent"));
    }

    [Fact]
    public void 锁定键可被识别()
    {
        var sub = new ConfigSubscription();
        sub.Lock("policy.disabledModules");
        Assert.True(sub.IsLocked("policy.disabledModules"));
        Assert.False(sub.IsLocked("display.railTrigger"));
    }

    [Fact]
    public void 前缀锁定覆盖整段()
    {
        var sub = new ConfigSubscription();
        sub.Lock("policy.");
        Assert.True(sub.IsLocked("policy.disabledModules"));
        Assert.True(sub.IsLocked("policy.anything"));
    }

    [Fact]
    public void 订阅方异常不影响其它订阅方()
    {
        var sub = new ConfigSubscription();
        var ok = 0;
        sub.Subscribe("a", () => throw new InvalidOperationException());
        sub.Subscribe("a", () => ok++);

        sub.Notify("a");
        Assert.Equal(1, ok);
    }

    [Fact]
    public void 退订后不再触发()
    {
        var sub = new ConfigSubscription();
        var hit = 0;
        Action h = () => hit++;
        sub.Subscribe("k", h);
        sub.Notify("k");
        Assert.True(sub.Unsubscribe("k", h));
        sub.Notify("k");
        Assert.Equal(1, hit);
    }
}
