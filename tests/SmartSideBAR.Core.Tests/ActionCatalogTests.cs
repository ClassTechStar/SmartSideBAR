// 插件契约 v1 (E1): 解析/校验/分层合并的纯逻辑测试。
using SmartSideBAR.Core.Actions;
using SmartSideBAR.Core.Rules;

namespace SmartSideBAR.Core.Tests;

public sealed class ActionCatalogTests
{
    private const string ValidPackage = """
    {
      "schemaVersion": 1,
      "id": "com.example.teaching",
      "name": "备课工具箱",
      "version": "1.0.0",
      "actions": [
        {
          "id": "teaching.openPlatform",
          "title": "打开智慧教育平台",
          "icon": "builtin:link",
          "group": "教学",
          "order": 10,
          "command": { "type": "open-url", "url": "https://www.zxx.edu.cn" }
        }
      ]
    }
    """;

    private static ActionCatalogResult Merge(string json, int priority = ActionSourcePriority.User, string? host = "1.8.0") =>
        ActionCatalog.Merge([new ActionSource("test.json", json, priority)], host);

    [Fact]
    public void 合法包解析出动作()
    {
        var result = Merge(ValidPackage);

        Assert.Empty(result.Failures);
        var action = Assert.Single(result.Actions);
        Assert.Equal("teaching.openPlatform", action.Id);
        Assert.Equal("builtin:link", action.Icon);
        Assert.Equal("open-url", action.Command!.Type);
        Assert.Single(result.Packages);
    }

    [Fact]
    public void 条件按现有规则模型解析()
    {
        var json = """
        {
          "schemaVersion": 1, "id": "com.example.cond", "name": "条件包",
          "actions": [{
            "id": "cond.onClass", "title": "上课时提示", "icon": "builtin:bell",
            "command": { "type": "toast", "text": "上课了", "level": "info" },
            "conditions": [{ "kind": "LessonState", "mode": "Equals", "value": "OnClass" }]
          }]
        }
        """;
        var result = Merge(json);

        Assert.Empty(result.Failures);
        var action = Assert.Single(result.Actions);
        Assert.Single(action.Conditions!);
        Assert.Equal(RuleConditionKind.LessonState, action.Conditions![0].Kind);
    }

    [Fact]
    public void 未知schema版本整包拒绝()
    {
        var result = Merge("""{ "schemaVersion": 9, "id": "com.example.v9", "name": "新版本", "actions": [ {"id":"x.y.z","title":"t","command":{"type":"toast","text":"t"}} ] }""");

        Assert.Empty(result.Actions);
        Assert.Contains(result.Failures, f => f.Reason.Contains("schemaVersion"));
    }

    [Fact]
    public void 非法或保留id被拒()
    {
        var badId = Merge("""{ "schemaVersion":1,"id":"Bad Id","name":"x","actions":[{"id":"a.b.c","title":"t","command":{"type":"toast","text":"t"}}]}""");
        Assert.Empty(badId.Actions);
        Assert.Contains(badId.Failures, f => f.Reason.Contains("扩展包 id"));

        var reserved = Merge("""{ "schemaVersion":1,"id":"com.example.ok","name":"x","actions":[{"id":"builtin.capture","title":"t","command":{"type":"toast","text":"t"}}]}""");
        Assert.Empty(reserved.Actions);
        Assert.Contains(reserved.Failures, f => f.Reason.Contains("保留前缀"));
    }

    [Fact]
    public void 未知命令类型只废单个动作()
    {
        var json = """
        {
          "schemaVersion": 1, "id": "com.example.mixed", "name": "混合包",
          "actions": [
            { "id": "mixed.run", "title": "启动程序", "command": { "type": "run-process", "path": "a.exe" } },
            { "id": "mixed.ok", "title": "可用动作", "command": { "type": "toast", "text": "ok" } }
          ]
        }
        """;
        var result = Merge(json);

        var action = Assert.Single(result.Actions);
        Assert.Equal("mixed.ok", action.Id);
        Assert.Contains(result.Failures, f => f.Id == "mixed.run" && f.Reason.Contains("不支持的命令类型"));
    }

    [Fact]
    public void 非http地址被拒()
    {
        var json = """{ "schemaVersion":1,"id":"com.example.url","name":"x","actions":[{"id":"url.bad","title":"t","command":{"type":"open-url","url":"javascript:alert(1)"}}]}""";
        Assert.Empty(Merge(json).Actions);
        Assert.Contains(Merge(json).Failures, f => f.Reason.Contains("http/https"));
    }

    [Fact]
    public void dispatch白名单外被拒()
    {
        var json = """{ "schemaVersion":1,"id":"com.example.disp","name":"x","actions":[{"id":"disp.bad","title":"t","command":{"type":"dispatch","action":"install-update"}}]}""";
        Assert.Empty(Merge(json).Actions);

        var ok = """{ "schemaVersion":1,"id":"com.example.disp2","name":"x","actions":[{"id":"disp.ok","title":"t","command":{"type":"dispatch","action":"workspace:上课"}}]}""";
        Assert.Single(Merge(ok).Actions);
    }

    [Fact]
    public void 重复动作id只保留一个()
    {
        const string a = """{ "schemaVersion":1,"id":"com.example.a","name":"A","actions":[{"id":"dup.action","title":"A","command":{"type":"toast","text":"a"}}]}""";
        const string b = """{ "schemaVersion":1,"id":"com.example.b","name":"B","actions":[{"id":"dup.action","title":"B","command":{"type":"toast","text":"b"}}]}""";
        var result = ActionCatalog.Merge(
        [
            new ActionSource("a.json", a, ActionSourcePriority.User),
            new ActionSource("b.json", b, ActionSourcePriority.User),
        ]);

        Assert.Single(result.Actions);
        Assert.Contains(result.Failures, f => f.Reason.Contains("动作 id 重复"));
    }

    [Fact]
    public void 高优先级包整体覆盖低优先级()
    {
        const string low = """{ "schemaVersion":1,"id":"com.example.same","name":"用户版","actions":[{"id":"same.one","title":"用户动作","command":{"type":"toast","text":"u"}}]}""";
        const string high = """{ "schemaVersion":1,"id":"com.example.same","name":"策略版","actions":[{"id":"same.two","title":"策略动作","command":{"type":"toast","text":"p"}}]}""";
        var result = ActionCatalog.Merge(
        [
            new ActionSource("user.json", low, ActionSourcePriority.User),
            new ActionSource("policy.json", high, ActionSourcePriority.Policy),
        ]);

        var action = Assert.Single(result.Actions);
        Assert.Equal("same.two", action.Id);
        Assert.Equal("策略版", Assert.Single(result.Packages).Name);
    }

    [Fact]
    public void 宿主版本不足时整包跳过()
    {
        var json = """{ "schemaVersion":1,"id":"com.example.future","name":"未来包","minHostVersion":"2.0.0","actions":[{"id":"future.x","title":"t","command":{"type":"toast","text":"t"}}]}""";
        var result = Merge(json, host: "1.8.0");

        Assert.Empty(result.Actions);
        Assert.Contains(result.Failures, f => f.Reason.Contains("需要宿主"));
    }

    [Fact]
    public void 超大文件被拒()
    {
        var padded = ValidPackage + new string(' ', ActionValidation.MaxFileBytes);
        var result = Merge(padded);

        Assert.Empty(result.Actions);
        Assert.Contains(result.Failures, f => f.Reason.Contains("256KB"));
    }

    [Fact]
    public void 路径必须落在允许根内()
    {
        var root = Path.Combine(Path.GetTempPath(), "ssb-actions-root");
        Directory.CreateDirectory(root);
        try
        {
            Assert.True(ActionValidation.IsAllowedPath(Path.Combine(root, "docs", "a.txt"), out var ok, root));
            Assert.NotNull(ok);
            Assert.False(ActionValidation.IsAllowedPath(Path.Combine(root, "..", "evil.txt"), out _, root));
            Assert.False(ActionValidation.IsAllowedPath(@"\\server\share\a.txt", out _, root));
            Assert.False(ActionValidation.IsAllowedPath("docs/a.txt", out _, root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
