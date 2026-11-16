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
