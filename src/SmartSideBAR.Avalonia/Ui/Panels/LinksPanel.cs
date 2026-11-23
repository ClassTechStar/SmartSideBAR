// LinksPanel 的 code-behind（M3: 内容移至 LinksPanel.axaml, 这里只做 VM 接线与入场动画）。
using Avalonia.Input;
using SmartSideBAR.Avalonia.Ui.Panels.ViewModels;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed partial class LinksPanel : SidebarPanel
{
    public LinksPanel(SidebarServices services, Palette palette, Action<LinkItem> open) : base(services, palette)
    {
        PanelTheme.Apply(Resources, palette);
        InitializeComponent();
        Vm = new LinksPanelViewModel(services, palette, open) { RefreshRequested = Refresh };
        DataContext = Vm;
    }

    internal LinksPanelViewModel Vm { get; }

    protected override void RefreshCore()
    {
        Vm.Reload();
        OnBuilt();
    }

    /// <summary>P2-3: 分类切换后播放横向入场动画；关闭动效时直接显示。</summary>
    protected override void OnBuilt()
    {
        if (Vm.EnterDirection == 0 || Bounds.Width <= 0) return;
        var animate = !Services.Config.Current.Appearance.ReduceMotion;
        _ = PageTransition.SlideInAsync(this, Bounds.Width, Vm.EnterDirection, animate);
    }

    private void OnUrlKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Vm.SubmitCommand.Execute(null);
        e.Handled = true;
    }
}
