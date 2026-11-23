// 关于页 VM: 版本/更新说明/自动检查/检查更新与安装闭环。
using SmartSideBAR.Avalonia.Updater;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

public sealed class AboutSettingsViewModel : SettingsPageViewModel
{
    private readonly ConfigService _config;
    private readonly ISettingsHost _host;
    private string _updateStatus = "";
    private bool _autoCheck;
    private bool _loading;
    private string? _downloadedInstaller;
    private bool _installVisible;

    public AboutSettingsViewModel(ConfigService config, ISettingsHost host)
    {
        _config = config;
        _host = host;

        Version = typeof(AboutSettingsViewModel).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "unknown";
        var notes = ReleaseNotes.ForVersion(Version) ?? ReleaseNotes.ForVersion(ReleaseNotes.CurrentVersion);
        NotesVersion = notes?.Version ?? Version;
        Notes = notes?.Changes.Take(6).Select(c => "• " + c).ToArray() ?? [];

        CheckUpdateCommand = new AsyncRelayCommand(() => CheckUpdateAsync(Version),
            onError: ex => UpdateStatus = $"检查更新失败: {ex.Message}");
        InstallCommand = new RelayCommand(_ =>
        {
            if (_downloadedInstaller is { } path) _host.InstallAndExit(path);
        });
    }

    public override string Title => "关于";

    public string Version { get; }

    public string NotesVersion { get; }

    public IReadOnlyList<string> Notes { get; }

    public bool HasNotes => Notes.Count > 0;

    public string UpdateStatus
    {
        get => _updateStatus;
        private set => SetProperty(ref _updateStatus, value);
    }

    public bool AutoCheck
    {
        get => _autoCheck;
        set
        {
            // 载入阶段只回填控件状态, 不触发落盘 (旧实现同样不写)
            if (SetProperty(ref _autoCheck, value) && !_loading) _ = _config.Set("updates.autoCheck", value);
        }
    }

    public bool InstallVisible
    {
        get => _installVisible;
        private set => SetProperty(ref _installVisible, value);
    }

    public string InstallText => _downloadedInstaller is null ? "立即安装" : "立即安装并重启";

    public AsyncRelayCommand CheckUpdateCommand { get; }

    public RelayCommand InstallCommand { get; }

    public override void LoadFrom(SidekickConfig config)
    {
        _loading = true;
        AutoCheck = config.Updates.AutoCheck;
        _loading = false;
    }

    public override IEnumerable<ConfigChange> CollectChanges() => [];

    private async Task CheckUpdateAsync(string currentVersion)
    {
        UpdateStatus = "正在检查更新…";
        _host.Toast("正在检查更新…", "info");
        var service = new UpdateService();
        var check = await service.CheckAsync(currentVersion);
        if (check.Error is { } checkError)
        {
            UpdateStatus = $"检查更新失败: {checkError}";
            _host.Toast($"检查更新失败: {checkError}", "error");
            return;
        }
        if (check.Info is not { } info)
        {
            UpdateStatus = "";
            _host.Toast("已经是最新版本", "success");
            return;
        }

        // P0 安全: 镜像通道无法提供独立于下载链路的可信哈希 → 不自动下载/安装
        if (!info.HashTrusted)
        {
            _downloadedInstaller = null;
            InstallVisible = false;
            UpdateStatus =
                $"发现 {info.Version}（{info.Channel}）。该通道无法独立校验安装包完整性，已停止自动下载；" +
                "请在 Release 页面手动获取，或换用可直连 GitHub 的网络后重试。";
            _host.Toast($"发现 {info.Version}，但 {info.Channel} 通道无法校验安装包来源，已停止自动下载", "warn");
            return;
        }

        UpdateStatus = $"发现 {info.Version}（{info.Channel}），正在下载…";
        var progress = new Progress<double>(p => UpdateStatus = $"发现 {info.Version}，正在下载… {p:0}%");
        var result = await service.DownloadAsync(info, progress);
        if (result.Error is { } downloadError)
        {
            UpdateStatus = $"下载失败: {downloadError}";
            _host.Toast($"{info.Version} 下载失败: {downloadError}，可到 Release 页面手动获取", "error");
            return;
        }
        _downloadedInstaller = result.Path;
        InstallVisible = true;
        UpdateStatus = $"已下载并通过 SHA256 校验 {info.Version}，点「立即安装」完成更新";
        _host.Toast($"已下载 {info.Version}（SHA256 校验通过）", "success");
    }
}
