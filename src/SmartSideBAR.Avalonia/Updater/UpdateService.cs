// 应用内更新检查 (P2-4)。
// 借鉴 CyreneCompass 的 updater.js + Rust check_update：
// 只做「检查 + 下载 + 启动安装包」，不静默替换正在运行的程序；
// 关闭开关时完全不联网，与现有 winget/Release 渠道互补。
using System.Net.Http;
using System.Text.Json;
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Avalonia.Updater;

public sealed record UpdateInfo(
    string Version,
    string? Url,
    string? FileName,
    long? FileSize,
    string? ReleaseUrl,
    string? Notes);

public sealed class UpdateService
{
    private const string Api = "https://api.github.com/repos/ClassTechStar/SmartSideBAR/releases/latest";
    private static readonly string DownloadDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartSideBAR", "updates");

    /// <summary>检查更新。currentVersion 之后有新版则返回 UpdateInfo，否则返回 null。</summary>
    public async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "SmartSideBAR");
            http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            using var resp = await http.GetAsync(Api, ct);
            if (!resp.IsSuccessStatusCode) return null;

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(tag)) return null;
            if (!VersionComparer.IsNewer(tag, currentVersion)) return null;

            string? url = null, fileName = null, releaseUrl = null;
            long? size = null;
            if (root.TryGetProperty("assets", out var assets))
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (!VersionComparer.IsInstaller(name)) continue;
                    url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                    fileName = name;
                    size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? sz : null;
                    break;
                }
            }
            if (root.TryGetProperty("html_url", out var h)) releaseUrl = h.GetString();
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;

            // 没有 Windows x64 安装包就不打扰用户
            if (url is null) return null;

            return new UpdateInfo(
                VersionComparer.Parse(tag).Length > 0 ? tag!.TrimStart('v', 'V') : tag!,
                url, fileName, size, releaseUrl, notes);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>下载安装包到本机更新目录，返回本地路径；失败返回 null。</summary>
    public async Task<string?> DownloadAsync(UpdateInfo info, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(info.Url)) return null;
        try
        {
            Directory.CreateDirectory(DownloadDir);
            var path = Path.Combine(DownloadDir, info.FileName ?? "SmartSideBAR-Setup.exe");
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "SmartSideBAR");
            await using var src = await http.GetStreamAsync(info.Url, ct);
            await using var dst = File.Create(path);
            await src.CopyToAsync(dst, ct);
            // 大小对不上说明下载不完整，直接判失败
            if (info.FileSize is { } expected)
            {
                var actual = new FileInfo(path).Length;
                if (actual != expected) return null;
            }
            return path;
        }
        catch
        {
            return null;
        }
    }
}
