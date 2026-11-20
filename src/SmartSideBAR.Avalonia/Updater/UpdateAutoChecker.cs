// 每日自动检查更新 (ClockHost 统一时钟驱动)。
// 边界: 自动部分止步于「检查 (+可选后台下载)」, 安装永远由用户在 设置→关于 点击 ——
// 绝不静默替换运行中的程序。演示模式 (SSB_DEMO=1) 与调试器下不自动检查, 弹窗会毁现场。
using System.Diagnostics;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Avalonia.Updater;

public static class UpdateAutoChecker
{
    private const string ConsumerName = "update-check";

    private static ConfigService? _config;
    private static string _currentVersion = "";
    private static int _busy; // 0/1 防重入 (下载通道慢时 24h tick 可能重叠)
    private static bool _registered;

    /// <summary>自动检查发现新版 (UI 订阅弹 Toast; 智能课堂静默规则照常生效)。</summary>
    public static event Action<UpdateInfo>? UpdateFound;

    /// <summary>纯判定 (测试用): 全部满足才自动检查。</summary>
    internal static bool ShouldAutoCheck(bool enabled, bool autoCheck, bool demo, bool debugger, bool busy) =>
        enabled && autoCheck && !demo && !debugger && !busy;

    public static void Init(ConfigService config, string currentVersion)
    {
        _config = config;
        _currentVersion = currentVersion;
        if (_registered) return;
        _registered = true;
        ClockHost.Register(ConsumerName, 24 * 3600 * 1000, () => _ = TickAsync());
    }

    private static async Task TickAsync()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;
        try
        {
            var cfg = _config?.Current;
            if (cfg is null) return;
            if (!ShouldAutoCheck(cfg.Updates.Enabled, cfg.Updates.AutoCheck,
                    DemoMode.Enabled, Debugger.IsAttached, busy: false)) return;

            var service = new UpdateService();
            var check = await service.CheckAsync(_currentVersion);
            if (check.Info is not { } info) return; // 已是最新或失败: 静默, 不打扰

            if (cfg.Updates.AutoDownload)
            {
                await service.DownloadAsync(info);
                ToastService.Instance.Show(
                    $"新版本 {info.Version} 已下载就绪，到 设置 → 关于 安装", "success");
            }
            else
            {
                ToastService.Instance.Show(
                    $"发现新版本 {info.Version}，到 设置 → 关于 检查并安装", "info");
            }
            UpdateFound?.Invoke(info);
        }
        catch
        {
            // 自动检查失败静默 (教室离线是常态), 用户手动检查会看到完整原因
        }
        finally
        {
            Interlocked.Exchange(ref _busy, 0);
        }
    }
}
