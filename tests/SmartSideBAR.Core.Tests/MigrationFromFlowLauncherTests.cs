// 本轮跨项目迁移新增的纯逻辑守卫：计时封装 / 代理解析。
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Core.Tests;

public class MigrationFromFlowLauncherTests
{
    // ---- TimedOperation（借鉴 Flow.Launcher Stopwatch.Debug/Info）----

    [Fact]
    public void 计时封装返回值与耗时()
    {
        var result = TimedOperation.Run(() => 42);
        Assert.Equal(42, result.Value);
        Assert.True(result.ElapsedMs >= 0);
    }

    [Fact]
    public async Task 计时封装支持无返回值与异步()
    {
        var sync = TimedOperation.Run(() => { });
        Assert.True(sync.Value);

        var asyncResult = await TimedOperation.RunAsync(async () => { await Task.Yield(); return 7; });
        Assert.Equal(7, asyncResult.Value);
    }

    [Fact]
    public void 超阈值判定()
    {
        var result = new TimedResult<int>(1, 100);
        Assert.True(result.Exceeds(50));
        Assert.False(result.Exceeds(100));
        Assert.False(result.Exceeds(0)); // 0 表示不判定
    }

    [Fact]
    public void 空委托被拒绝()
    {
        Assert.Throws<ArgumentNullException>(() => TimedOperation.Run((Func<int>)null!));
    }

    // ---- ProxyEnvironment（借鉴 Flow.Launcher HttpProxy）----

    [Fact]
    public void 代理按优先级解析()
    {
        var env = new Dictionary<string, string?>
        {
            ["HTTPS_PROXY"] = "http://proxy-a:8080",
            ["https_proxy"] = "http://proxy-b:8080",
            ["HTTP_PROXY"] = "http://proxy-c:8080",
        };
        Assert.Equal("http://proxy-a:8080", ProxyEnvironment.Resolve(k => env.GetValueOrDefault(k)));

        env.Remove("HTTPS_PROXY");
        Assert.Equal("http://proxy-b:8080", ProxyEnvironment.Resolve(k => env.GetValueOrDefault(k)));
    }

    [Fact]
    public void 空白值视为未设置()
    {
        var env = new Dictionary<string, string?> { ["HTTPS_PROXY"] = "   " };
        Assert.Null(ProxyEnvironment.Resolve(k => env.GetValueOrDefault(k)));
    }

    [Fact]
    public void 仅HTTP与HTTPS地址可用()
    {
        Assert.True(ProxyEnvironment.IsUsable("http://127.0.0.1:7890"));
        Assert.True(ProxyEnvironment.IsUsable("https://proxy.example.com"));
        Assert.False(ProxyEnvironment.IsUsable("socks5://127.0.0.1:1080")); // WebProxy 不支持 socks
        Assert.False(ProxyEnvironment.IsUsable("not a url"));
        Assert.False(ProxyEnvironment.IsUsable(null));
    }
}
