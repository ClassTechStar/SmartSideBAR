// 本轮跨项目迁移的 UI 层守卫：SVG 图标缓存 / 异常链展开。
using SmartSideBAR.Avalonia;
using SmartSideBAR.Avalonia.Ui;

namespace SmartSideBAR.Avalonia.Tests;

public class FlowLauncherMigrationTests
{
    [Fact]
    public void 同一图标重复加载命中缓存()
    {
        SvgIcons.ClearCache();
        var first = SvgIcons.Load("capture", white: true);
        var second = SvgIcons.Load("capture", white: true);
        Assert.Same(first, second);
        Assert.Equal(1, SvgIcons.CacheCount);
    }

    [Fact]
    public void 明暗两套图标分别缓存()
    {
        SvgIcons.ClearCache();
        SvgIcons.Load("link", white: false);
        SvgIcons.Load("link", white: true);
        Assert.Equal(2, SvgIcons.CacheCount);
    }

    [Fact]
    public void 不存在的图标也缓存空结果避免反复解析()
    {
        SvgIcons.ClearCache();
        Assert.Null(SvgIcons.Load("__no_such_icon__"));
        Assert.Equal(1, SvgIcons.CacheCount);
    }

    [Fact]
    public void 异常链逐层展开()
    {
        var innermost = new InvalidOperationException("root cause");
        var middle = new ArgumentException("middle", innermost);
        var outer = new Exception("outer", middle);

        var chain = CrashLog.BuildExceptionChain(outer);

        Assert.Equal(3, chain.Count);
        Assert.Equal("outer", chain[0]["message"]);
        Assert.Equal("middle", chain[1]["message"]);
        Assert.Equal("root cause", chain[2]["message"]);
    }

    [Fact]
    public void 单层异常与空异常安全()
    {
        Assert.Single(CrashLog.BuildExceptionChain(new Exception("only")));
        Assert.Empty(CrashLog.BuildExceptionChain(null));
    }
}
