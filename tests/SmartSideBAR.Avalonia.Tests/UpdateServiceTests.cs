// UpdateService 纯函数守卫 (P0-4): SHA256SUMS 解析 / 镜像 URL 改写 / 资产选择。
// 这些逻辑决定"下载的东西是不是真的、是不是完整的", 必须有测试兜底。
using System.Text.Json;
using SmartSideBAR.Avalonia.Updater;

namespace SmartSideBAR.Avalonia.Tests;

public sealed class UpdateServiceTests
{
    private const string Hash64 = "94296fbb1d053726a9f1119493f48a9c0b4156ea753ee234cb3b0c4de64ea429";

    [Fact]
    public void 解析SHA256SUMS取目标文件哈希()
    {
        var content = $"{Hash64}  SmartSideBAR-v1.6.0-Setup.exe\n" +
                      "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa  other.exe\n";
        var hash = UpdateService.ParseSha256Sums(content, "SmartSideBAR-v1.6.0-Setup.exe");
        Assert.Equal(Hash64, hash);
    }

    [Fact]
    public void 解析SHA256SUMS兼容二进制标记与大小写()
    {
        var content = $"{Hash64.ToUpperInvariant()} *SmartSideBAR-v1.6.0-Setup.exe\r\n";
        var hash = UpdateService.ParseSha256Sums(content, "smartsidebar-v1.6.0-setup.EXE");
        Assert.Equal(Hash64, hash);
    }

    [Fact]
    public void 解析SHA256SUMS缺目标或格式非法返回null()
    {
        Assert.Null(UpdateService.ParseSha256Sums($"{Hash64}  other.exe\n", "SmartSideBAR-v1.6.0-Setup.exe"));
        Assert.Null(UpdateService.ParseSha256Sums("not-a-hash  SmartSideBAR-v1.6.0-Setup.exe\n", "SmartSideBAR-v1.6.0-Setup.exe"));
        Assert.Null(UpdateService.ParseSha256Sums("", "SmartSideBAR-v1.6.0-Setup.exe"));
        Assert.Null(UpdateService.ParseSha256Sums($"{Hash64}  SmartSideBAR-v1.6.0-Setup.exe\n", null));
    }

    [Fact]
    public void 镜像通道改写下载URL直连不改()
    {
        const string url = "https://github.com/x/y/releases/download/v1/a.exe";
        Assert.Equal(url, UpdateService.RewriteUrl(url, null));
        Assert.Equal("https://gh-proxy.com/" + url, UpdateService.RewriteUrl(url, "https://gh-proxy.com/"));
        Assert.Null(UpdateService.RewriteUrl(null, "https://gh-proxy.com/"));
    }

    [Fact]
    public void 资产选择优先当前架构安装包并跳过非安装包()
    {
        using var doc = JsonDocument.Parse("""
        {
          "assets": [
            { "name": "SHA256SUMS.txt", "browser_download_url": "https://h/SHA256SUMS.txt", "size": 10 },
            { "name": "SmartSideBAR-v1.6.0-Setup.exe", "browser_download_url": "https://h/x64.exe", "size": 100 },
            { "name": "SmartSideBAR-v1.6.0-arm64-Setup.exe", "browser_download_url": "https://h/arm.exe", "size": 200 }
          ]
        }
        """);
        var (url, name, size) = UpdateService.SelectInstallerAsset(doc.RootElement, null);
        Assert.NotNull(name);
        Assert.EndsWith("-Setup.exe", name);
        Assert.NotNull(url);
        Assert.Contains(size, new long?[] { 100L, 200L }); // 只会在两个安装包里选, 不会误选 SHA256SUMS
    }

    [Fact]
    public void 资产选择无安装包返回空()
    {
        using var doc = JsonDocument.Parse("""{ "assets": [ { "name": "README.md", "browser_download_url": "https://h/r.md" } ] }""");
        var (url, name, _) = UpdateService.SelectInstallerAsset(doc.RootElement, null);
        Assert.Null(url);
        Assert.Null(name);
    }

    [Fact]
    public void 找SHA256SUMS资产地址支持镜像前缀()
    {
        using var doc = JsonDocument.Parse("""
        {
          "assets": [
            { "name": "SmartSideBAR-v1.6.0-Setup.exe", "browser_download_url": "https://h/a.exe" },
            { "name": "SHA256SUMS.txt", "browser_download_url": "https://h/SHA256SUMS.txt" }
          ]
        }
        """);
        Assert.Equal("https://h/SHA256SUMS.txt", UpdateService.FindAssetUrl(doc.RootElement, null, "SHA256SUMS.txt", "SHA256SUMS"));
        Assert.Equal("p/https://h/SHA256SUMS.txt", UpdateService.FindAssetUrl(doc.RootElement, "p/", "SHA256SUMS.txt", "SHA256SUMS"));
        Assert.Null(UpdateService.FindAssetUrl(doc.RootElement, null, "SHA256SUMS"));
    }
}

// ---- 更新功能补齐回归 (digest 兜底 / Gitee 通道 / 自动检查门槛 / 静默安装) ----
public sealed class UpdateFeatureTests
{
    private const string Hash64 = "94296fbb1d053726a9f1119493f48a9c0b4156ea753ee234cb3b0c4de64ea429";

    [Fact]
    public void Gitee通道指向真实镜像仓库()
    {
        // 回归钉: 此前指向 ClassTechStar/SmartSideBAR —— Gitee 上不存在该仓库,
        // 通道永远 404; 真实镜像在 qualcommsnapdragon 名下
        var gitee = UpdateService.Channels.Single(c => c.Name == "Gitee");
        Assert.Contains("qualcommsnapdragon/SmartSideBAR", gitee.Api);
        Assert.DoesNotContain("api.github.com", gitee.Api);
    }

    [Fact]
    public void 资产自带Digest可提取为哈希()
    {
        var json = """
    {"tag_name":"v1.7.0","assets":[
      {"name":"SmartSideBAR-v1.7.0-x64-Setup.exe","size":1024,
       "browser_download_url":"https://example.com/setup.exe",
       "digest":"sha256:94296FBB1D053726A9F1119493F48A9C0B4156EA753EE234CB3B0C4DE64EA429"}
    ]}
    """;
        using var doc = JsonDocument.Parse(json);
        var hash = UpdateService.GetAssetDigest(doc.RootElement, "SmartSideBAR-v1.7.0-x64-Setup.exe");
        Assert.Equal(Hash64, hash); // 小写归一
    }

    [Fact]
    public void 非法Digest返回null()
    {
        var json = """
    {"tag_name":"v1.7.0","assets":[
      {"name":"SmartSideBAR-v1.7.0-x64-Setup.exe","size":1024,
       "browser_download_url":"https://example.com/setup.exe","digest":"md5:deadbeef"}
    ]}
    """;
        using var doc = JsonDocument.Parse(json);
        Assert.Null(UpdateService.GetAssetDigest(doc.RootElement, "SmartSideBAR-v1.7.0-x64-Setup.exe"));
    }

    [Fact]
    public void 目标文件不存在时Digest返回null()
    {
        var json = """{"tag_name":"v1.7.0","assets":[]}""";
        using var doc = JsonDocument.Parse(json);
        Assert.Null(UpdateService.GetAssetDigest(doc.RootElement, "anything.exe"));
    }

    [Fact]
    public void 静默安装参数含必要开关()
    {
        // /VERYSILENT 静默 /CLOSEAPPLICATIONS 兜底 /NORESTART 不重启系统;
        // 绝不带 /FORCECLOSEAPPLICATIONS —— 不强杀用户正在用的程序
        Assert.Contains("/VERYSILENT", UpdateService.SilentInstallArguments);
        Assert.Contains("/CLOSEAPPLICATIONS", UpdateService.SilentInstallArguments);
        Assert.DoesNotContain("/FORCECLOSEAPPLICATIONS", UpdateService.SilentInstallArguments);
    }

    [Theory]
    [InlineData(true, true, false, false, false, true)]   // 全部满足
    [InlineData(false, true, false, false, false, false)] // 总开关关
    [InlineData(true, false, false, false, false, false)] // 自动检查关
    [InlineData(true, true, true, false, false, false)]   // 演示模式
    [InlineData(true, true, false, true, false, false)]   // 调试器 (开发机)
    [InlineData(true, true, false, false, true, false)]   // 上一轮仍在跑
    public void 自动检查门槛按序判定(
        bool enabled, bool autoCheck, bool demo, bool debugger, bool busy, bool expected)
    {
        Assert.Equal(expected, UpdateAutoChecker.ShouldAutoCheck(enabled, autoCheck, demo, debugger, busy));
    }
}
