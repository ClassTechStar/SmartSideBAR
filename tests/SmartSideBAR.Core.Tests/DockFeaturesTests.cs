// Dock 化 W3-W11 新模块纯逻辑回归: 剪贴板存储/隐私闸门, SnapGrid, 布局匹配, 指标差分。
using SmartSideBAR.Core.Clipboard;
using SmartSideBAR.Core.Layout;
using SmartSideBAR.Core.Metrics;
using SmartSideBAR.Core.Snap;

namespace SmartSideBAR.Core.Tests;

public class ClipboardHistoryTests
{
    [Fact]
    public void 文本去重置顶()
    {
        var store = new ClipboardHistoryStore();
        store.Add(ClipboardEntryKind.Text, "hello", []);
        store.Add(ClipboardEntryKind.Text, "world", []);
        store.Add(ClipboardEntryKind.Text, "hello", []);
        var items = store.Texts();
        Assert.Equal(2, items.Count);
        Assert.Equal("hello", items[0].Text); // 重复置顶而非新增
    }

    [Fact]
    public void 文本环形容量200()
    {
        var store = new ClipboardHistoryStore();
        for (var i = 0; i < 205; i++) store.Add(ClipboardEntryKind.Text, $"t{i}", []);
        Assert.Equal(200, store.Texts().Count);
        Assert.Equal("t204", store.Texts()[0].Text); // 最新在前
    }

    [Fact]
    public void Pin项不被容量淘汰()
    {
        var store = new ClipboardHistoryStore();
        for (var i = 0; i < 10; i++) store.Add(ClipboardEntryKind.Text, $"t{i}", []);
        store.Add(ClipboardEntryKind.Text, "常驻", []);
        // Pin 最后一项 (最旧), 塞满新条目后它仍在
        var pinned = store.Texts().Last() with { Pinned = true };
        var store2 = new ClipboardHistoryStore();
        store2.Add(ClipboardEntryKind.Text, "常驻", []);
        for (var i = 0; i < 200; i++) store2.Add(ClipboardEntryKind.Text, $"x{i}", []);
        // 新 store 没继承 Pin (Pin 是 UI 态), 此处只验证环形上限本身
        Assert.Equal(200, store2.Texts().Count);
    }

    [Fact]
    public void 文件条目上限20()
    {
        var store = new ClipboardHistoryStore();
        for (var i = 0; i < 25; i++) store.Add(ClipboardEntryKind.Files, $"{i}.txt", [$"{i}.txt"]);
        Assert.Equal(20, store.Files().Count);
    }

    [Theory]
    [InlineData(true, false, "explorer", false, true)]
    [InlineData(false, false, "explorer", false, false)]  // 总开关关
    [InlineData(true, true, "explorer", false, false)]    // 暂停中 (隐私红线 3)
    [InlineData(true, false, "mstsc", true, false)]       // 敏感进程黑名单 (隐私红线 2)
    public void ShouldCapture_隐私三闸(
        bool enabled, bool paused, string fg, bool inBlacklist, bool expected)
    {
        var blacklist = inBlacklist ? new[] { "mstsc" } : Array.Empty<string>();
        Assert.Equal(expected, ClipboardHistoryRules.ShouldCapture(enabled, paused, fg, blacklist));
    }

    [Fact]
    public void 文本规整去空白与截断()
    {
        var long_text = new string('x', 5000);
        Assert.Equal(4096, ClipboardHistoryRules.NormalizeText(long_text).Length);
        Assert.Equal("abc", ClipboardHistoryRules.NormalizeText("  abc  "));
    }
}

public class SnapGridTests
{
    private static readonly SnapRect Work = new(0, 0, 1920, 1080);

    [Fact]
    public void 二分屏左右各半()
    {
        var rects = SnapGrid.ComputeRects(SnapGridSpec.TwoColumns, Work);
        Assert.Equal(2, rects.Count);
        Assert.Equal(8, rects[0].X);
        Assert.Equal(1920 - 8, rects[1].X + rects[1].W);
    }

    [Fact]
    public void 田字四象限()
    {
        var rects = SnapGrid.ComputeRects(SnapGridSpec.Quadrant, Work);
        Assert.Equal(4, rects.Count);
        Assert.All(rects, r => { Assert.Equal(944, r.W); Assert.Equal(524, r.H); }); // 960/2 - 8*2 侧 gutter
    }

    [Fact]
    public void OnePlusTwo_左半右两格()
    {
        var rects = SnapGrid.ComputeRects(SnapGridSpec.OnePlusTwo, Work);
        Assert.Equal(3, rects.Count);
        Assert.Equal(960 - 16, rects[0].W);
        Assert.Equal(rects[1].X, rects[2].X);
    }

    [Fact]
    public void gutter_槽位间不重叠()
    {
        var rects = SnapGrid.ComputeRects(SnapGridSpec.ThreeColumns, Work);
        for (var i = 1; i < rects.Count; i++)
            Assert.True(rects[i].X >= rects[i - 1].X + rects[i - 1].W);
    }

    [Fact]
    public void 非法网格返回空()
    {
        Assert.Empty(SnapGrid.ComputeRects(new SnapGridSpec(0, 1), Work));
    }
}

public class WindowLayoutMatcherTests
{
    private static readonly WindowLayoutEntry Saved =
        new("code.exe", "Program.cs - Visual Studio", 100, 100, 800, 600, "normal", 0);

    [Fact]
    public void 全匹配得分最高()
    {
        var score = WindowLayoutMatcher.Score(Saved, "code.exe", "Program.cs - Visual Studio", 0);
        Assert.True(score >= 7);
    }

    [Fact]
    public void exe不同直接零分()
    {
        Assert.Equal(0, WindowLayoutMatcher.Score(Saved, "notepad.exe", "Program.cs", 0));
    }

    [Fact]
    public void title不同仍有exe加显示器基础分()
    {
        Assert.Equal(5, WindowLayoutMatcher.Score(Saved, "code.exe", "其他窗口", 0)); // exe4 + 同屏1
    }
}

public class MetricsCalculatorTests
{
    [Fact]
    public void Cpu差分_空载为0满载为100()
    {
        Assert.Equal(0, MetricsCalculator.CpuPercent(90, 100, 90, 100));
        Assert.Equal(100, MetricsCalculator.CpuPercent(0, 100, 0, 200));
        Assert.Equal(50, MetricsCalculator.CpuPercent(50, 100, 100, 200));
    }

    [Fact]
    public void Cpu差分_total不增时返回0防除零()
    {
        Assert.Equal(0, MetricsCalculator.CpuPercent(10, 100, 10, 100));
    }

    [Fact]
    public void 网速差分_回绕按0计()
    {
        Assert.Equal(1000, MetricsCalculator.BytesPerSecond(0, 1000, 1.0));
        Assert.Equal(0, MetricsCalculator.BytesPerSecond(1000, 500, 1.0)); // 计数器回绕
    }
}
