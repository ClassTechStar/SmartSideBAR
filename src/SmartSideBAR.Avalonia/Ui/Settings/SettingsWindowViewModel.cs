// 设置窗 VM (M2): 导航索引 + 分页 VM 集合 + 保存请求。
// 页面不直接碰配置; 保存时把所有页面的 ConfigChange 合并成一次 ApplyMany。
using SmartSideBAR.Avalonia.Ui.Settings.ViewModels;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Ui.Settings;

public sealed class SettingsWindowViewModel : ObservableObject
{
    private int _selectedIndex;

    public SettingsWindowViewModel(IReadOnlyList<string> titles, IReadOnlyList<SettingsPageViewModel> pages)
    {
        Titles = titles;
        Pages = pages;
        SaveCommand = new RelayCommand(_ => SaveRequested?.Invoke());
    }

    /// <summary>导航标题 (含仅展示页, 如自动化/设计规范)。</summary>
    public IReadOnlyList<string> Titles { get; }

    /// <summary>可保存的分页 VM (顺序与窗口里的页面视图一一对应)。</summary>
    public IReadOnlyList<SettingsPageViewModel> Pages { get; }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    public RelayCommand SaveCommand { get; }

    /// <summary>保存按钮点击 → 宿主统一落盘并关闭。</summary>
    public event Action? SaveRequested;

    public void LoadFrom(SidekickConfig config)
    {
        foreach (var page in Pages) page.LoadFrom(config);
    }

    public IReadOnlyList<ConfigChange> CollectChanges() =>
        Pages.SelectMany(p => p.CollectChanges()).ToArray();
}
