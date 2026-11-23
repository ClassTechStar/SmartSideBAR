// 插件契约 E2: 宿主扫描 (用户/策略目录) 与执行器白名单路由的测试。
using SmartSideBAR.Avalonia.Actions;
using SmartSideBAR.Core.Actions;

namespace SmartSideBAR.Avalonia.Tests;

public sealed class ActionHostTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "ssb-actions-" + Guid.NewGuid().ToString("N")[..8]);

    public ActionHostTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string UserDir => Path.Combine(_dir, "user");

    private string PolicyDir => Path.Combine(_dir, "policy");

    private static string Package(string packageId, string actionId, string command = "{\"type\":\"toast\",\"text\":\"ok\"}") => $$"""
    {
      "schemaVersion": 1,
      "id": "{{packageId}}",
      "name": "测试包",
      "actions": [{ "id": "{{actionId}}", "title": "测试动作", "command": {{command}} }]
    }
    """;

    private void Write(string dir, string file, string json)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, file), json);
    }

    [Fact]
    public void 用户目录动作包被加载()
    {
        Write(UserDir, "a.json", Package("com.example.user", "user.action"));

        var result = ActionHost.Load([], UserDir, null, "1.8.0");

        Assert.Empty(result.Failures);
        Assert.Equal("user.action", Assert.Single(result.Actions).Id);
    }

    [Fact]
    public void 策略目录同包覆盖用户包()
    {
        Write(UserDir, "u.json", Package("com.example.same", "same.user"));
        Write(PolicyDir, "p.json", Package("com.example.same", "same.policy"));

        var result = ActionHost.Load([], UserDir, PolicyDir, "1.8.0");

        Assert.Equal("same.policy", Assert.Single(result.Actions).Id);
    }

    [Fact]
    public void 非法JSON记入失败不影响其他包()
    {
        Write(UserDir, "bad.json", "{ not json");
        Write(UserDir, "good.json", Package("com.example.good", "good.action"));

        var result = ActionHost.Load([], UserDir, null, "1.8.0");

        Assert.Single(result.Actions);
        Assert.Contains(result.Failures, f => f.Origin.Contains("bad.json"));
    }

    [Fact]
    public void 超大文件在读取前被拒()
    {
        Write(UserDir, "huge.json", Package("com.example.huge", "huge.action") +
                                   new string(' ', ActionValidation.MaxFileBytes));

        var result = ActionHost.Load([], UserDir, null, "1.8.0");

        Assert.Empty(result.Actions);
        Assert.Contains(result.Failures, f => f.Reason.Contains("256KB"));
    }

    [Fact]
    public void 执行器按白名单路由到宿主能力()
    {
        var sink = new FakeSink();
        var executor = new ActionExecutor(sink);

        Assert.True(executor.TryExecute(Action("a.open", new ActionCommand(ActionCommandTypes.OpenUrl, Url: "https://x.cn")), out _));
        Assert.True(executor.TryExecute(Action("a.dispatch", new ActionCommand(ActionCommandTypes.Dispatch, Action: "capture")), out _));
        Assert.True(executor.TryExecute(Action("a.ws", new ActionCommand(ActionCommandTypes.Workspace, Workspace: "上课")), out _));
        Assert.True(executor.TryExecute(Action("a.copy", new ActionCommand(ActionCommandTypes.CopyText, Text: "板书")), out _));
        Assert.True(executor.TryExecute(Action("a.toast", new ActionCommand(ActionCommandTypes.Toast, Text: "提示", Level: "warn")), out _));

        Assert.Equal(["https://x.cn"], sink.Urls);
        Assert.Equal(["capture", "workspace:上课"], sink.Dispatched);
        Assert.Equal(["板书"], sink.Copied);
        Assert.Equal([("提示", "warn")], sink.Toasts);
    }

    [Fact]
    public void 未知命令与越界路径被拒绝且不落地()
    {
        var sink = new FakeSink();
        var executor = new ActionExecutor(sink);

        Assert.False(executor.TryExecute(Action("a.run", new ActionCommand("run-process", Path: "a.exe")), out var runError));
        Assert.Contains("不支持", runError);

        Assert.False(executor.TryExecute(Action("a.path", new ActionCommand(ActionCommandTypes.OpenPath, Path: @"\\server\share")), out _));
        Assert.Empty(sink.Paths);
    }

    private static ActionDefinition Action(string id, ActionCommand command) =>
        new(id, "测试动作", Command: command);

    private sealed class FakeSink : IActionSink
    {
        public List<string> Urls { get; } = [];
        public List<string> Paths { get; } = [];
        public List<string> Copied { get; } = [];
        public List<string> Dispatched { get; } = [];
        public List<(string Message, string Level)> Toasts { get; } = [];

        public void OpenUrl(string url) => Urls.Add(url);

        public void OpenPath(string path) => Paths.Add(path);

        public void CopyText(string text) => Copied.Add(text);

        public void Toast(string message, string level) => Toasts.Add((message, level));

        public void Dispatch(string action) => Dispatched.Add(action);
    }
}
