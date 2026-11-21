// 代理环境解析（借鉴 Flow.Launcher 的 HttpProxy 配置）。
// 纯逻辑：从环境变量解析代理地址，Core 层可单测；Avalonia 的 UpdateService 直接用结果。
// 约定：HTTPS_PROXY > https_proxy > HTTP_PROXY > http_proxy，显式空串视为未设置。
namespace SmartSideBAR.Core.Diagnostics;

public static class ProxyEnvironment
{
    /// <summary>按优先级返回第一个有效的代理 URL；无设置返回 null。</summary>
    public static string? Resolve(Func<string, string?>? lookup = null)
    {
        lookup ??= Environment.GetEnvironmentVariable;
        foreach (var name in new[] { "HTTPS_PROXY", "https_proxy", "HTTP_PROXY", "http_proxy" })
        {
            var value = lookup(name);
            if (!string.IsNullOrWhiteSpace(value)) return value!.Trim();
        }
        return null;
    }

    /// <summary>URL 是否为可用于 WebProxy 的绝对地址。</summary>
    public static bool IsUsable(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
