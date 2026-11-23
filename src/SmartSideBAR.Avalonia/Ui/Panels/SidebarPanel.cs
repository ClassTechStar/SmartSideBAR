// 侧栏面板基类, P2-1 从 SidebarPanels.cs 拆出; M3 起支持 XAML 面板。
// 面板刷新统一走 Refresh()（合并请求 + 延后构建），
// 避免在控件自身的事件派发中销毁可视树, 那是原生崩溃的来源之一。
using Avalonia.Controls;
using SmartSideBAR.Avalonia.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal abstract class SidebarPanel : UserControl
{
    private bool _pending;
    private Palette _palette;

    protected SidebarPanel(SidebarServices services, Palette palette)
    {
        Services = services;
        _palette = palette;
    }

    protected SidebarServices Services { get; }

    protected Palette Palette => _palette;

    /// <summary>
    /// 主题切换: 就地重写面板级资源字典并刷新 VM 的调色板快照（图标染色/状态色）,
    /// 不重建可视树。M4 主题隔离; 由 SidebarWindow.ApplyTheme 调用。
    /// </summary>
    internal void ApplyPalette(Palette palette)
    {
        _palette = palette;
        PanelTheme.Apply(Resources, palette);
        if (DataContext is ViewModels.PanelViewModel vm) vm.ApplyPalette(palette);
    }

    /// <summary>代码构建面板的内容工厂。XAML 面板（M3）内容由 InitializeComponent 完成,
    /// 不需要重写本方法, 改为重写 RefreshCore 刷新 VM。
    /// 注意: 不要在子控件的事件处理器里同步重建面板, 控件会在派发事件过程中被销毁,
    /// Avalonia 的原生渲染层会因此崩溃（无托管异常、无崩溃日志）。统一走 Refresh()。</summary>
    protected virtual Control? Build() => null;

    /// <summary>内容构建完成后的钩子（例如启动/停止计时器）。</summary>
    protected virtual void OnBuilt() { }

    /// <summary>刷新核心：代码面板默认重建 Content；XAML 面板重写为「Vm.Reload() + OnBuilt()」。</summary>
    protected virtual void RefreshCore()
    {
        var content = Build();
        if (content is not null) Content = content;
        OnBuilt();
    }

    /// <summary>请求刷新：合并重复请求并延后到 Background 优先级执行。</summary>
    public void Refresh()
    {
        if (_pending) return;
        _pending = true;
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _pending = false;
            RefreshCore();
        }, global::Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>同步刷新（headless 测试用: Background 优先级的 Post 在 RunJobs 下不排空）。</summary>
    internal void RefreshNow()
    {
        _pending = false;
        RefreshCore();
    }
}
