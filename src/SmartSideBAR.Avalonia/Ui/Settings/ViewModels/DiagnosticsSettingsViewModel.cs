// 诊断页 VM: 自检 / 导出诊断包 / 配置导出导入。
// 文件选择与提示走 ISettingsHost (由设置窗实现), 本 VM 不直接依赖 Window。
using System.Text.Json;
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

public sealed class DiagnosticsSettingsViewModel : SettingsPageViewModel
{
    private readonly ConfigService _config;
    private readonly IDiagnosticsService _diag;
    private readonly ISettingsHost _host;
    private string _diagnosticsText = "";

    public DiagnosticsSettingsViewModel(ConfigService config, IDiagnosticsService diag, ISettingsHost host)
    {
        _config = config;
        _diag = diag;
        _host = host;

        RunCommand = new AsyncRelayCommand(RunAsync,
            onError: ex => DiagnosticsText = $"诊断失败: {ex.Message}");
        ExportPackCommand = new AsyncRelayCommand(ExportPackAsync);
        ExportConfigCommand = new AsyncRelayCommand(ExportConfigAsync);
        ImportConfigCommand = new AsyncRelayCommand(ImportConfigAsync);
    }

    public override string Title => "诊断";

    public string DiagnosticsText
    {
        get => _diagnosticsText;
        private set => SetProperty(ref _diagnosticsText, value);
    }

    public AsyncRelayCommand RunCommand { get; }

    public AsyncRelayCommand ExportPackCommand { get; }

    public AsyncRelayCommand ExportConfigCommand { get; }

    public AsyncRelayCommand ImportConfigCommand { get; }

    public override void LoadFrom(SidekickConfig config) { }

    public override IEnumerable<ConfigChange> CollectChanges() => [];

    private async Task RunAsync()
    {
        DiagnosticsText = "巡检中…";
        var result = await Task.Run(() => _diag.RunFullCheck());
        DiagnosticsText = string.Join('\n', result.Select(kv => $"{kv.Key}: {kv.Value}"));
    }

    private async Task ExportPackAsync()
    {
        try
        {
            // 日志文件被占用时 ZipFile 会抛 IOException; 必须兜住, 否则 UI 线程异常杀进程
            var path = await Task.Run(() => _diag.ExportPack(Path.GetTempPath()));
            _host.Toast($"诊断包: {path}", "success");
        }
        catch (Exception ex)
        {
            _host.Toast($"导出诊断包失败: {ex.Message}", "error");
        }
    }

    private async Task ExportConfigAsync()
    {
        try
        {
            var node = JsonSerializer.SerializeToNode(_config.Current, ConfigJsonContext.Default.SidekickConfig);
            if (node is null) return;
            var json = ConfigTransfer.Export(node, desensitize: true);
            var path = await _host.PickSaveJsonAsync("smartsidebar-config.json");
            if (path is null) return;
            await File.WriteAllTextAsync(path, json);
            _host.Toast($"配置已导出: {Path.GetFileName(path)}", "success");
        }
        catch (Exception ex)
        {
            _host.Toast($"导出配置失败: {ex.Message}", "error");
        }
    }

    private async Task ImportConfigAsync()
    {
        try
        {
            var path = await _host.PickOpenJsonAsync();
            if (path is null) return;
            var text = await File.ReadAllTextAsync(path);
            var imported = ConfigTransfer.Import(text);
            if (imported is null)
            {
                _host.Toast("配置文件无法解析", "error");
                return;
            }

            var applied = 0;
            foreach (var (configPath, value) in ConfigTransfer.EnumerateLeaves(imported))
            {
                if (_config.Subscriptions.IsLocked(configPath)) continue;
                if (configPath.Equals("version", StringComparison.OrdinalIgnoreCase)) continue;
                if (_config.Set(configPath, value)) applied++;
            }
            _host.Toast($"已导入 {applied} 项配置（未知键已忽略）", "success");
        }
        catch (Exception ex)
        {
            _host.Toast($"导入配置失败: {ex.Message}", "error");
        }
    }
}
