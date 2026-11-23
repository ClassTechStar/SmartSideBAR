// M3: 侧栏面板 VM 基类。
// 面板 code-behind 只负责「套调色板 + 载入 XAML + 转发刷新」, 状态与动作都在 VM 里。
using Avalonia.Media;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Avalonia.ViewModels.Infrastructure;

namespace SmartSideBAR.Avalonia.Ui.Panels.ViewModels;

internal abstract class PanelViewModel(SidebarServices services, Palette palette) : ObservableObject
{
    private Palette _palette = palette;

    protected SidebarServices Services { get; } = services;

    protected Palette Palette => _palette;

    /// <summary>由面板订阅: VM 内部动作结束后请求一次延后刷新（不在事件派发中替换可视树）。</summary>
    public Action? RefreshRequested { get; set; }

    /// <summary>主题相关开关变更后请求窗口重新套用主题。</summary>
    public event Action? ThemeChangeRequested;

    /// <summary>主题切换: 更新调色板快照并重载派生状态（图标染色/状态色画刷）。</summary>
    public void ApplyPalette(Palette palette)
    {
        _palette = palette;
        Reload();
    }

    protected void RequestRefresh() => RefreshRequested?.Invoke();

    protected void RequestThemeApply() => ThemeChangeRequested?.Invoke();

    /// <summary>重新读取配置/设备状态, 刷新绑定字段。</summary>
    public abstract void Reload();

    protected static IBrush Brush(Color color) => new SolidColorBrush(color);
}
