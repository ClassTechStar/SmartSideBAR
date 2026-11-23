// 应用内更新检查 (P2-4)。
// 借鉴 CyreneCompass 的 updater.js + Rust check_update：
// 只做「检查 + 下载 + 启动安装包」，不静默替换正在运行的程序；
// 关闭开关时完全不联网，与现有 winget/Release 渠道互补。
// P0-4 加固:
//   1) 完整性校验: Release 附 SHA256SUMS.txt, 下载后按哈希逐项校验 (不再只对文件大小);
//   2) 单例 HttpClient: 静态共享, 不再每次检查/下载都 new;
//   3) 多通道探测: GitHub API → 镜像代理 → Gitee 同步发布 (校园网直连 GitHub 不稳是常态),
//      同步给出失败原因, 不再静默返回 null;
//   4) 下载带进度回调 (校园网下 30MB 无进度体验极差)。
// 安全模型 (2026-11 审计修正):
//   哈希必须来自「独立于安装包下载链路」的信任源。镜像代理若能替换安装包,
//   同样能替换它转发的 SUMS 文件与 API 响应 —— 所以镜像通道不提供任何哈希,
//   HashTrusted=false, 调用方不得自动下载/安装; 只有作者自有源 (GitHub 直连 / Gitee)
//   取到的 digest 或 SUMS 才可信。直连拿到的哈希可用于校验镜像回落的安装包。
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Avalonia.Updater;

public sealed record UpdateInfo(
    string Version,
    string? Url,
    string? FileName,
    long? FileSize,
    string? Sha256,
    bool HashTrusted,
    string? ReleaseUrl,
    string? Notes,
    string Channel,
    string? FallbackUrl = null);

/// <summary>检查结果: Info=有新版; Error=失败原因; 两者皆空=已是最新。</summary>
public sealed record UpdateCheckResult(UpdateInfo? Info, string? Error);

/// <summary>下载结果: Path=安装包本地路径; Error=失败原因; Verified=是否通过可信哈希校验。</summary>
public sealed record UpdateDownloadResult(string? Path, string? Error, bool Verified = false);

public sealed class UpdateService
{
    private const string RepoApiPath = "repos/ClassTechStar/SmartSideBAR/releases/latest";

    /// <summary>
    /// 多通道探测顺序 (P0-4)。镜像通道的 DownloadPrefix 用于把资产下载 URL 一并走镜像;
    /// Gitee 为同步发布仓库, 资产 URL 由其 API 直接给出。
    /// </summary>
    internal static readonly (string Name, string Api, string? DownloadPrefix)[] Channels =
    [
        ("GitHub", $"https://api.github.com/{RepoApiPath}", null),
        ("GitHub镜像", $"https://gh-proxy.com/https://api.github.com/{RepoApiPath}", "https://gh-proxy.com/"),
        ("GitHub镜像2", $"https://ghproxy.net/https://api.github.com/{RepoApiPath}", "https://ghproxy.net/"),
        ("Gitee", "https://gitee.com/api/v5/repos/qualcommsnapdragon/SmartSideBAR/releases/latest", null),
    ];

    private static readonly HttpClient Http = CreateClient();

    private static readonly string DownloadDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SmartSideBAR", "updates");

    private static HttpClient CreateClient()
    {
        // 借鉴 Flow.Launcher 的 HttpProxy：教室/校园网常走代理，环境变量是最通用的入口。
        // 解析逻辑在 Core 纯函数里（可单测）；只有可用地址才覆盖 .NET 默认系统代理。
        var handler = new HttpClientHandler();
        var proxyUrl = global::SmartSideBAR.Core.Diagnostics.ProxyEnvironment.Resolve();
        if (global::SmartSideBAR.Core.Diagnostics.ProxyEnvironment.IsUsable(proxyUrl))
        {
            handler.Proxy = new System.Net.WebProxy(new Uri(proxyUrl!));
            handler.UseProxy = true;
        }

        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.Add("User-Agent", "SmartSideBAR");
        http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
        return http;
    }

    /// <summary>
    /// 检查更新。currentVersion 之后有新版返回 Info; 已是最新返回两者皆空;
    /// 所有通道均失败返回 Error (含各通道原因)。
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(string currentVersion, CancellationToken ct = default)
    {
        var errors = new List<string>();
        foreach (var (name, api, _) in Channels)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                using var resp = await Http.GetAsync(api, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    errors.Add($"{name}: HTTP {(int)resp.StatusCode}");
                    continue;
                }

                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                var root = doc.RootElement;

                var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
                if (string.IsNullOrWhiteSpace(tag))
                {
                    errors.Add($"{name}: 响应缺少版本标签");
                    continue;
                }
                if (!VersionComparer.IsNewer(tag, currentVersion))
                {
                    return new UpdateCheckResult(null, null); // 任一通道确认已是最新即止
                }

                // 资产地址一律取原始 URL: 镜像只在「直连拿到可信哈希」后作为下载回落,
                // 不再把安装包与其哈希清单一起交给同一个代理改写。
                var (url, fileName, size) = SelectInstallerAsset(root, null);
                if (url is null)
                {
                    errors.Add($"{name}: 该版本没有 Windows 安装包资产");
                    continue;
                }

                // 哈希信任源: 只有作者自有源 (GitHub 直连 / Gitee 同步发布) 才算数。
                // 镜像通道的 API 响应与 SUMS 都可能被代理替换, 不取任何哈希。
                var trustedChannel = IsTrustedHashChannel(name);
                string? sha256 = null;
                if (trustedChannel)
                {
                    var sumsUrl = FindAssetUrl(root, null, "SHA256SUMS.txt", "SHA256SUMS");
                    if (sumsUrl is not null)
                    {
                        try
                        {
                            var sums = await Http.GetStringAsync(sumsUrl, ct);
                            sha256 = ParseSha256Sums(sums, fileName);
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{name}: SHA256SUMS 获取失败 ({ex.Message})");
                        }
                    }
                    sha256 ??= GetAssetDigest(root, fileName);
                }

                // 直连成功时给安装包留一条镜像下载回落; 校验仍用直连拿到的哈希。
                var fallbackUrl = name == "GitHub" && sha256 is not null && url is not null
                    ? "https://gh-proxy.com/" + url
                    : null;

                var releaseUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() : null;
                var notes = root.TryGetProperty("body", out var b) ? b.GetString() : null;

                return new UpdateCheckResult(
                    new UpdateInfo(
                        VersionComparer.Parse(tag).Length > 0 ? tag!.TrimStart('v', 'V') : tag!,
                        url, fileName, size, sha256, HashTrusted: sha256 is not null,
                        releaseUrl, notes, name, fallbackUrl),
                    null);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors.Add($"{name}: {ex.Message}");
            }
        }
        return new UpdateCheckResult(null, "所有更新通道均不可达: " + string.Join("; ", errors));
    }

    /// <summary>
    /// 下载安装包到本机更新目录并按 SHA256 校验完整性, 返回本地路径。
    /// progress 回报 0-100 进度; 失败返回带原因的 Error。
    /// 主地址失败时按 FallbackUrl (镜像) 重试一次, 校验仍用调用方提供的可信哈希。
    /// </summary>
    public async Task<UpdateDownloadResult> DownloadAsync(
        UpdateInfo info, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(info.Url)) return new(null, "下载地址为空");
        // 完整性分级: 有哈希 → SHA256 强校验; 无哈希 → 下载长度与资产声明比对。
        // 长度不可知时拒绝 —— 宁可让用户手动下载也不装不明文件。
        var hashMode = !string.IsNullOrWhiteSpace(info.Sha256);
        if (!hashMode && info.FileSize is not > 0)
        {
            return new(null, "发布资产未提供哈希且大小未知，完整性无法校验，请到 Release 页面手动获取");
        }

        Directory.CreateDirectory(DownloadDir);
        var path = Path.Combine(DownloadDir, info.FileName ?? "SmartSideBAR-Setup.exe");
        var sources = new List<string> { info.Url };
        if (!string.IsNullOrWhiteSpace(info.FallbackUrl) &&
            !string.Equals(info.FallbackUrl, info.Url, StringComparison.OrdinalIgnoreCase))
        {
            sources.Add(info.FallbackUrl);
        }

        string? lastError = null;
        foreach (var source in sources)
        {
            try
            {
                using var resp = await Http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    lastError = $"下载失败: HTTP {(int)resp.StatusCode}";
                    continue;
                }

                var total = resp.Content.Headers.ContentLength ?? info.FileSize;
                await using (var src = await resp.Content.ReadAsStreamAsync(ct))
                await using (var dst = File.Create(path))
                {
                    var buffer = new byte[81920];
                    long read = 0;
                    int n;
                    while ((n = await src.ReadAsync(buffer.AsMemory(0, buffer.Length), ct)) > 0)
                    {
                        await dst.WriteAsync(buffer.AsMemory(0, n), ct);
                        read += n;
                        if (total is > 0) progress?.Report(Math.Min(100, read * 100.0 / total.Value));
                    }
                }

                // 完整性校验: 防截断/防篡改, 不匹配即删除, 不给用户留可疑文件
                if (hashMode)
                {
                    string hash;
                    await using (var fs = File.OpenRead(path))
                    {
                        hash = Convert.ToHexString(await SHA256.HashDataAsync(fs, ct)).ToLowerInvariant();
                    }
                    if (!string.Equals(hash, info.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Delete(path);
                        return new(null, "完整性校验失败 (SHA256 不匹配)，已删除下载文件");
                    }
                }
                else if (info.FileSize is { } declared && new FileInfo(path).Length != declared)
                {
                    File.Delete(path);
                    return new(null, "完整性校验失败 (文件大小与声明不符)，已删除下载文件");
                }
                progress?.Report(100);
                return new(path, null, Verified: hashMode);
            }
            catch (OperationCanceledException)
            {
                return new(null, "已取消");
            }
            catch (Exception ex)
            {
                lastError = $"下载失败: {ex.Message}";
                try { if (File.Exists(path)) File.Delete(path); } catch { /* 残留文件下次覆盖 */ }
            }
        }
        return new(null, lastError ?? "下载失败");
    }

    /// <summary>
    /// 该通道能否作为哈希的信任源。只有直连 GitHub 与作者自有 Gitee 镜像算数:
    /// 第三方镜像既能替换安装包, 也能替换它自己转发的 SUMS/API 响应。
    /// </summary>
    internal static bool IsTrustedHashChannel(string channel) => channel is "GitHub" or "Gitee";

    /// <summary>从 Release 资产里挑出 Windows 安装包; 优先匹配当前架构, 镜像通道用 prefix 改写下载 URL。</summary>
    internal static (string? Url, string? FileName, long? Size) SelectInstallerAsset(JsonElement root, string? prefix)
    {
        var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            _ => "x64",
        };
        (string? Url, string? FileName, long? Size) fallback = (null, null, null);
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (!VersionComparer.IsInstaller(name)) continue;
                var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
                var size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var sz) ? (long?)sz : null;
                var picked = (RewriteUrl(url, prefix), name, size);
                if (name is not null && name.Contains(arch, StringComparison.OrdinalIgnoreCase)) return picked;
                fallback = picked;
            }
        }
        return fallback;
    }

    /// <summary>找指定名字的资产下载地址 (取 SHA256SUMS 用)。</summary>
    internal static string? FindAssetUrl(JsonElement root, string? prefix, params string[] names)
    {
        if (!root.TryGetProperty("assets", out var assets)) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (name is null) continue;
            if (!names.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase))) continue;
            var url = asset.TryGetProperty("browser_download_url", out var u) ? u.GetString() : null;
            return RewriteUrl(url, prefix);
        }
        return null;
    }

    /// <summary>镜像通道: 资产 URL 也要走同一镜像前缀 (gh-proxy 系按「前缀 + 原 URL」取件)。</summary>
    internal static string? RewriteUrl(string? url, string? prefix) =>
        url is null || prefix is null ? url : prefix + url;

    /// <summary>
    /// 解析 SHA256SUMS 文本 (格式: 「哈希␠␠文件名」, 兼容 GNU sha256sum 与 *binary 标记),
    /// 返回 fileName 对应的哈希; 找不到返回 null。
    /// </summary>
    /// <summary>
    /// 资产自带摘要兜底 (GitHub API 2024 起为资产计算 digest, 形如 "sha256:HEX")。
    /// SUMS 文件缺失时用; 非 sha256 前缀或长度非法返回 null。
    /// </summary>
    internal static string? GetAssetDigest(System.Text.Json.JsonElement root, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;
        if (!root.TryGetProperty("assets", out var assets)) return null;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
            if (!string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)) continue;
            var digest = asset.TryGetProperty("digest", out var d) ? d.GetString() : null;
            if (string.IsNullOrWhiteSpace(digest) || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return null;
            var hex = digest["sha256:".Length..].Trim();
            if (hex.Length != 64 || !hex.All(c => Uri.IsHexDigit(c))) return null;
            return hex.ToLowerInvariant();
        }
        return null;
    }

    /// <summary>静默安装参数: 应用自身先退出释放文件锁; /CLOSEAPPLICATIONS 兜底; 不重启系统。</summary>
    internal const string SilentInstallArguments = "/VERYSILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS /NORESTART";

    /// <summary>
    /// 启动已校验的安装包 (静默)。调用方在启动后自行退出 —— Inno 的 [Run] 条目
    /// 会在安装完成后把应用拉起来。返回 false 表示启动失败 (文件丢失等)。
    /// </summary>
    public static bool LaunchInstaller(string installerPath)
    {
        if (string.IsNullOrWhiteSpace(installerPath) || !File.Exists(installerPath)) return false;
        var psi = new System.Diagnostics.ProcessStartInfo(installerPath)
        {
            UseShellExecute = true,
            Arguments = SilentInstallArguments,
        };
        System.Diagnostics.Process.Start(psi);
        return true;
    }

    internal static string? ParseSha256Sums(string content, string? fileName)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(fileName)) return null;
        foreach (var raw in content.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var sep = line.IndexOfAny([' ', '\t']);
            if (sep <= 0) continue;
            var hash = line[..sep].Trim();
            var name = line[sep..].Trim().TrimStart('*', ' ', '\t');
            if (hash.Length != 64) continue;
            if (!string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)) continue;
            return hash.ToLowerInvariant();
        }
        return null;
    }
}
