// 侧栏面板基类, P2-1 从 SidebarPanels.cs 拆出。
// 面板只实现 Build()；刷新统一走 Refresh()（合并请求 + 延后构建），
// 避免在控件自身的事件派发中销毁可视树, 那是原生崩溃的来源之一。
using Avalonia.Controls;
using SmartSideBAR.Avalonia.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal abstract class SidebarPanel : UserControl
{
    private bool _pending;

    protected SidebarPanel(SidebarServices services, Palette palette)
    {
        Services = services;
        Palette = palette;
    }

    protected SidebarServices Services { get; }

    protected Palette Palette { get; }

    /// <summary>构建面板内容。每次刷新都会重新构建（面板内容轻量，可整体替换）。
        /// 注意: 不要在子控件的事件处理器里同步重建面板, 控件会在派发事件过程中被销毁,
    /// Avalonia 的原生渲染层会因此崩溃（无托管异常、无崩溃日志）。统一走 Refresh()。</summary>
    protected abstract Control Build();

    /// <summary>内容构建完成后的钩子（例如启动/停止计时器）。</summary>
    protected virtual void OnBuilt() { }

    /// <summary>请求刷新：合并重复请求并延后到 Background 优先级执行。</summary>
    public void Refresh()
    {
        if (_pending) return;
        _pending = true;
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _pending = false;
            Content = Build();
            OnBuilt();
        }, global::Avalonia.Threading.DispatcherPriority.Background);
    }

    /// <summary>同步重建（headless 测试用: Background 优先级的 Post 在 RunJobs 下不排空）。</summary>
    internal void RefreshNow()
    {
        _pending = false;
        Content = Build();
        OnBuilt();
    }

    protected StackPanel Stack(double spacing = 12) => new() { Spacing = spacing };
}
