// 配置导入导出 (P2-5): JSON 往返与敏感键脱敏。
using System.Text.Json.Nodes;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Core.Tests;

public class ConfigTransferTests
{
    private static JsonObject Sample() => new()
    {
        ["version"] = 2,
        ["capture"] = new JsonObject { ["dir"] = @"C:\Users\someone\Pictures\Sidekick" },
        ["recorder"] = new JsonObject { ["dir"] = @"D:\Private\Videos" },
        ["display"] = new JsonObject { ["railTrigger"] = "always" },
        ["policy"] = new JsonObject { ["disabledModules"] = new JsonArray("usb") },
    };

    [Fact]
    public void 导出会脱敏本机路径与策略段()
    {
        var json = ConfigTransfer.Export(Sample(), desensitize: true);
        var node = JsonNode.Parse(json)!;

        Assert.Null(node["capture"]!["dir"]);
        Assert.Null(node["recorder"]!["dir"]);
        Assert.Null(node["policy"]);
        // 非敏感键保留
        Assert.Equal("always", node["display"]!["railTrigger"]!.GetValue<string>());
    }

    [Fact]
    public void 不脱敏时保留原值()
    {
        var json = ConfigTransfer.Export(Sample(), desensitize: false);
        var node = JsonNode.Parse(json)!;
        Assert.NotNull(node["capture"]!["dir"]);
    }

    [Fact]
    public void 导入合法JSON可解析()
    {
        var node = ConfigTransfer.Import("""{"version":2,"display":{"railTrigger":"edge"}}""");
        Assert.NotNull(node);
        Assert.Equal("edge", node!["display"]!["railTrigger"]!.GetValue<string>());
    }

    [Fact]
    public void 非法输入返回空而不抛异常()
    {
        Assert.Null(ConfigTransfer.Import(""));
        Assert.Null(ConfigTransfer.Import("   "));
        Assert.Null(ConfigTransfer.Import("{ 不是 json"));
    }
}
