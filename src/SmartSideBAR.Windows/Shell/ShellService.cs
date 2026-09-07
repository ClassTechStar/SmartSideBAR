// Windows/Shell/ShellService.cs —— §6.8: taskmgr / 外链白名单 (B2 根修) / HKCU 自启 (P1-10 asInvoker 路径)。
using System.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Windows.Shell;

public interface IShellService
{
    void OpenTaskManager();
    void OpenExternal(Uri url);
    void OpenPath(string path);
    void RevealInExplorer(string path);
    void SetAutoLaunch(bool enable);
    bool GetAutoLaunch();
}

public sealed class ShellService(ConfigService config, ILogger<ShellService>? log = null) : IShellService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "SmartSideBAR";

    // B2 白名单: https/http + 内置运维域名 + config.links 中用户配置的域名
    private static readonly string[] BuiltinHosts =
    [
        "www.zxx.edu.cn", "easinote.seewo.com", "www.zxxk.com", "wenku.baidu.com", "www.cnki.net",
    ];

    public void OpenTaskManager() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("taskmgr.exe") { UseShellExecute = true });

    public void OpenExternal(Uri url)
    {
        if (url.Scheme is not ("http" or "https")) throw new SecurityException($"URL 协议不在白名单: {url.Scheme}");
        if (!IsAllowedHost(url.Host)) throw new SecurityException($"URL 域名不在白名单: {url.Host}");
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url.ToString()) { UseShellExecute = true });
    }

    public void OpenPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Directory.Exists(path) && !File.Exists(path)) throw new FileNotFoundException(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
    }

    public void RevealInExplorer(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
        {
            UseShellExecute = true,
        });
    }

    public void SetAutoLaunch(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enable)
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) throw new InvalidOperationException("ProcessPath 不可用");
            key.SetValue(AppName, $"\"{exe}\" /silent");
            log?.LogInformation("[Shell] 已启用自启");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
            log?.LogInformation("[Shell] 已禁用自启");
        }
    }

    public bool GetAutoLaunch()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(AppName) is string;
    }

    private bool IsAllowedHost(string host)
    {
        var links = config.Current.Links.Select(l => l.Url)
            .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) ? uri.Host : "")
            .Where(h => h.Length > 0);
        return BuiltinHosts.Contains(host, StringComparer.OrdinalIgnoreCase)
            || links.Contains(host, StringComparer.OrdinalIgnoreCase);
    }
}
