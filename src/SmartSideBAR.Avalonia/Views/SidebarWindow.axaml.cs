using Avalonia.Controls;
using Avalonia.Layout;
using AvaloniaVisual = global::Avalonia.Visual;
using Avalonia.VisualTree;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Windows.AppBar;

namespace SmartSideBAR.Avalonia.Views;

public partial class SidebarWindow : Window
{
    private readonly AppBarEdge _side;

    // 供运行时 XAML 加载器可达; 应用路径一律走带 edge 的构造
    public SidebarWindow() : this(AppBarEdge.Right) { }

    public SidebarWindow(AppBarEdge side)
    {
        _side = side;
        InitializeComponent();
        ApplySide();
    }

    /// <summary>系统最近授予的总宽度 (物理像素, 含 rail)。</summary>
    public int LastGrantedWidthPx { get; private set; }

    public void OnAppBarGeometry(int grantedWidthPx)
    {
        LastGrantedWidthPx = grantedWidthPx;
    }

    /// <summary>
    /// B16 根修: XAML 里 rail 固定 Right / 面板固定 Left, 侧栏配到左侧时展开态会把 rail 甩到
    /// 屏幕内侧、面板盖住 rail。这里按停靠边镜像两者的对齐方式。
    /// </summary>
    private void ApplySide()
    {
        var rail = this.FindControl<Border>("RailCapsule");
        var panel = this.FindControl<Border>("PanelHost");
        if (rail is not null)
        {
            rail.HorizontalAlignment = _side == AppBarEdge.Left
                ? HorizontalAlignment.Left
                : HorizontalAlignment.Right;
        }
        if (panel is not null)
        {
            panel.HorizontalAlignment = _side == AppBarEdge.Left
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
        }
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        LoadSvgIcons(this);
        if (DataContext is SidebarViewModel vm) vm.RefreshIme();
    }

    /// <summary>Tag="svg:名称" → Assets/icons/名称.svg (1.1 图标, 深色); "svgw:" → 白色变体。
    /// 仅用于 XAML 静态标记的图标; 代码构造的控件请直接调用 SvgIcons.Load。</summary>
    internal static void LoadSvgIcons(AvaloniaVisual root)
    {
        if (!root.IsAttachedToVisualTree()) return;
        foreach (var image in root.GetVisualDescendants().OfType<Image>())
        {
            if (image.Tag is not string tag) continue;
            var (prefix, white) = tag.StartsWith("svgw:") ? ("svgw:", true) : ("svg:", false);
            if (!tag.StartsWith(prefix)) continue;
            var source = Ui.SvgIcons.Load(tag[prefix.Length..], white);
            if (source is not null) image.Source = source;
        }
    }
}
