// 数据驱动变更说明（借鉴 SidebarDiagnostics 的 ChangeLog.json + 版本比对）。
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Core.Tests;

public class ReleaseNotesTests
{
    private const string Markdown = """
        # 更新日志

        说明文字。

        ## [1.6.0] - 2026-10
        ### 新增
        - 离线墨迹转文字
        - 智能课堂
        ### 修复
        - CI 覆盖率门禁

        ## [1.5.0] - 2026-09
        - 侧栏主形态定稿
        """;

    [Fact]
    public void 解析版本与条目()
    {
        var entries = ReleaseNotes.Parse(Markdown);
        Assert.Equal(2, entries.Count);
        Assert.Equal("1.6.0", entries[0].Version);
        Assert.Equal("2026-10", entries[0].Date);
        Assert.Equal(3, entries[0].Changes.Count);
        Assert.Equal("离线墨迹转文字", entries[0].Changes[0]);
    }

    [Fact]
    public void 按版本查找()
    {
        var entries = ReleaseNotes.Parse(Markdown);
        Assert.Equal("1.5.0", ReleaseNotes.Find(entries, "1.5.0")!.Version);
        Assert.Null(ReleaseNotes.Find(entries, "9.9.9"));
    }

    [Fact]
    public void 版本号归一化()
    {
        Assert.Equal("1.6.0", ReleaseNotes.Normalize("v1.6.0"));
        Assert.Equal("1.6.0", ReleaseNotes.Normalize("1.6.0.0"));
        Assert.Equal("1.6.0", ReleaseNotes.Normalize("1.6.0+abc123"));
        Assert.Equal("", ReleaseNotes.Normalize(null));
    }

    [Fact]
    public void 空文本与无版本段落安全返回()
    {
        Assert.Empty(ReleaseNotes.Parse(""));
        Assert.Empty(ReleaseNotes.Parse("只有一行说明，没有版本标题"));
    }

    [Fact]
    public void 嵌入的CHANGELOG可读到当前版本条目()
    {
        // 运行时数据源就是人工维护的 CHANGELOG.md，不另维护一份 JSON
        var entry = ReleaseNotes.ForVersion(ReleaseNotes.CurrentVersion);
        Assert.NotNull(entry);
        Assert.True(entry!.Changes.Count > 0);
    }
}
