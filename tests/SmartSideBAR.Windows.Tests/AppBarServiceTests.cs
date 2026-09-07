// AppBarServiceTests —— 通知状态机全链路断言 (方案 §5.6 验收 V1-V8 的自动化层 + §7.1 示例)
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Tests.TestDoubles;
using Xunit;

namespace SmartSideBAR.Windows.Tests;

public sealed class AppBarServiceTests
{
    private const nint Hwnd = 0x1234;
    private const uint CallbackMsg = AppBarNative.AppBarCallbackMsg;
    private const int Rail = 64;
    private const int Expanded = 420;

    private static (nuint WParam, nint LParam) N(uint wp, int lp = 0) => (wp, lp);

    [Fact]
    public void Attach_SelfHeals_Registers_AndPositionsRail()
    {
        var (svc, api, hook, _, geometry, _, _) = TestEnv.CreateService();

        Assert.True(svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded));

        // V6 自愈: 注册前先补发一次 ABM_REMOVE 清孤儿注册
        Assert.Single(api.RemoveCalls);
        Assert.Equal([("NEW", CallbackMsg)], api.RegisterCalls.Select(r => ("NEW", r.CallbackMsg)).ToList());
        Assert.Contains("QUERYPOS", api.Ops);
        Assert.Contains("SETPOS", api.Ops);
        Assert.Contains("WINDOWPOSCHANGED", api.Ops);
        // 首次以 rail 宽度落位 (物理像素, K4) —— v1.1 悬浮胶囊: 上下各留 8% 工作区
        var first = Assert.Single(api.MoveRequests);
        Assert.Equal(Hwnd, first.Hwnd);
        Assert.Equal(Rail, first.Rect.W);
        Assert.Equal(874, first.Rect.H);                      // 工作区 84% 胶囊高
        Assert.Equal(83, first.Rect.Y);                       // 顶部 8% 边距
        Assert.Equal(1920 - Rail, first.Rect.X);              // 右缘贴齐
        Assert.True(hook.HasHook(Hwnd));                      // K1 闭环的前提: 钩子已挂
        Assert.Single(geometry);                              // AppBarGeometryChanged 已广播
        Assert.True(svc.IsRegistered);
    }

    [Fact]
    public void Attach_RegisterFails_FallsBack_NoHookNoMove()
    {
        var (svc, api, hook, _, _, _, _) = TestEnv.CreateService();
        api.RegisterResult = false;

        Assert.False(svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded));
        Assert.False(svc.IsRegistered);
        Assert.False(hook.HasHook(Hwnd)); // 失败不挂钩子
        Assert.Empty(api.MoveRequests);   // 失败不落位
        Assert.Single(api.RegisterCalls);
    }

    [Fact]
    public void Attach_RightEdge_QueryRect_AnchorsToFullMonitor()
    {
        var (svc, api, _, _, _, _, _) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);
        var q = Assert.Single(api.QueryCalls);
        Assert.Equal(AppBarNative.ABE_RIGHT, q.Edge);
        // 勘误 (方案 §5.3): 锚点为 rcMonitor 全屏矩形 —— rcWork 会引发占位带逐次内漂
        Assert.Equal(1920 - Rail, q.Want.X);
        Assert.Equal(Rail, q.Want.W);
        Assert.Equal(874, q.Want.H);  // 胶囊高提议 (工作区 84%)
    }

    [Fact]
    public void Reposition_IsStable_UnderRepeatedPosChanged()
    {
        var (svc, api, hook, _, _, _, _) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);

        // 模拟注册期 ABN_POSCHANGED 风暴: 授予矩形必须恒定, 不允许向屏幕内侧漂移
        for (var i = 0; i < 6; i++)
        {
            hook.Simulate(Hwnd, CallbackMsg, AppBarNative.ABN_POSCHANGED, 0);
        }

        Assert.Equal(7, api.MoveRequests.Count);
        Assert.All(api.MoveRequests, m =>
        {
            Assert.Equal(1920 - Rail, m.Rect.X);
            Assert.Equal(Rail, m.Rect.W);
            Assert.Equal(874, m.Rect.H);   // 反复重排不漂移、不蚕食
        });
    }

    [Fact]
    public void AbnPosChanged_RequeriesAndRepositions()
    {
        var (svc, api, hook, _, _, _, _) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);
        var before = api.QueryCalls.Count;

        // 模拟任务栏换边等 WorkArea 变化 (K1)
        var (result, handled) = hook.Simulate(Hwnd, CallbackMsg, AppBarNative.ABN_POSCHANGED, 0);

        Assert.True(handled);
        Assert.Equal(0, result);
        Assert.Equal(before + 1, api.QueryCalls.Count); // 重协商发生
        Assert.Equal(2, api.MoveRequests.Count);
        Assert.Equal(Rail, api.MoveRequests[^1].Rect.W);
    }

    [Fact]
    public void AbnFullscreen_CollapsesToRail_RestoresUserPrefOnExit()
    {
        var (svc, api, hook, _, _, fullscreen, _) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);
        svc.SetExpanded(true); // 用户偏好: 展开
        Assert.Equal(Expanded, api.MoveRequests[^1].Rect.W);

        // 全屏授课视频启动 (K3 / V5)
        hook.Simulate(Hwnd, CallbackMsg, AppBarNative.ABN_FULLSCREENAPP, 1);
        Assert.True(svc.FullscreenActive);
        Assert.Equal(Rail, api.MoveRequests[^1].Rect.W);   // 收起为 rail
        Assert.Single(fullscreen);

        // 全屏结束 → 按用户偏好恢复展开
        hook.Simulate(Hwnd, CallbackMsg, AppBarNative.ABN_FULLSCREENAPP, 0);
        Assert.False(svc.FullscreenActive);
        Assert.Equal(Expanded, api.MoveRequests[^1].Rect.W);
        Assert.Equal(2, fullscreen.Count);
    }

    [Fact]
    public void AbnStateChange_Repositions()
    {
        var (svc, api, hook, _, _, _, _) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);
        var before = api.MoveRequests.Count;
        hook.Simulate(Hwnd, CallbackMsg, AppBarNative.ABN_STATECHANGE, 0);
        Assert.Equal(before + 1, api.MoveRequests.Count);
    }

    [Fact]
    public void AbnWindowArrange_PublishesPending()
    {
        var (svc, _, hook, _, _, _, arrange) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);
        hook.Simulate(Hwnd, CallbackMsg, AppBarNative.ABN_WINDOWARRANGE, 1);
        Assert.Single(arrange);
        Assert.True(arrange[0].Hiding);
    }

    [Fact]
    public void WmActivate_ReportsActivateToSystem()
    {
        var (svc, api, hook, _, _, _, _) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);
        hook.Simulate(Hwnd, AppBarNative.WM_ACTIVATE, 1, 0);
        Assert.Single(api.ActivateCalls); // K2: 激活上报
    }

    [Fact]
    public void WmDisplayChange_Repositions()
    {
        var (svc, api, hook, _, _, _, _) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);
        var before = api.QueryCalls.Count;
        hook.Simulate(Hwnd, AppBarNative.WM_DISPLAYCHANGE, 0, 0); // K7: 分辨率变化
        Assert.Equal(before + 1, api.QueryCalls.Count);
    }

    [Fact]
    public void OtherMessages_PassThroughUnhandled()
    {
        var (svc, _, hook, _, _, _, _) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);
        var (_, handled) = hook.Simulate(Hwnd, 0x0084 /*WM_NCHITTEST*/, 0, 0);
        Assert.False(handled); // 不拦截无关消息
    }

    [Fact]
    public void SetExpanded_MovesToExpandedWidth()
    {
        var (svc, api, _, _, geometry, _, _) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);
        var geometryBefore = geometry.Count;
        svc.SetExpanded(true);
        Assert.Equal(Expanded, api.MoveRequests[^1].Rect.W);
        Assert.Equal(geometryBefore + 1, geometry.Count);
        Assert.Equal(Expanded, geometry[^1].WidthPx);
        svc.SetExpanded(false);
        Assert.Equal(Rail, api.MoveRequests[^1].Rect.W);
    }

    [Fact]
    public void Detach_RestoresSystemWorkArea()
    {
        var (svc, api, hook, _, _, _, _) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);
        var removesAfterAttach = api.RemoveCalls.Count;

        svc.Detach();

        Assert.False(svc.IsRegistered);
        Assert.False(hook.HasHook(Hwnd));            // 钩子已摘
        Assert.Equal(removesAfterAttach + 1, api.RemoveCalls.Count); // V8: 注销
        svc.Detach();  // 幂等
        Assert.Equal(removesAfterAttach + 1, api.RemoveCalls.Count);
    }

    [Fact]
    public void Dispose_DetachesIdempotently()
    {
        var (svc, api, _, _, _, _, _) = TestEnv.CreateService();
        svc.Attach(Hwnd, AppBarEdge.Right, Rail, Expanded);
        svc.Dispose();
        Assert.False(svc.IsRegistered);
        svc.Dispose();
    }
}
