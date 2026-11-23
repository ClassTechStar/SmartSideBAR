// M2 设置页分页基类: 每页一个 VM, 负责「载入快照 → 双向绑定 → 收集变更」。
// 页面不再直接读 ConfigService; 保存由 SettingsWindow 统一批量落盘。
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

public abstract class SettingsPageViewModel : ObservableObject
{
    public abstract string Title { get; }

    /// <summary>用当前配置填充页面字段 (打开设置窗时调用一次)。</summary>
    public abstract void LoadFrom(SidekickConfig config);

    /// <summary>收集本页需要写入的配置变更 (保存时统一 ApplyMany)。</summary>
    public abstract IEnumerable<ConfigChange> CollectChanges();
}

/// <summary>设置页需要的宿主能力 (文件选择/提示/安装), 由 SettingsWindow 实现。</summary>
public interface ISettingsHost
{
    Task<string?> PickSaveJsonAsync(string suggestedFileName);

    Task<string?> PickOpenJsonAsync();

    void Toast(string message, string level);

    /// <summary>启动已校验的安装包并退出应用。</summary>
    void InstallAndExit(string installerPath);
}
