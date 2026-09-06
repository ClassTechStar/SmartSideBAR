using Avalonia.Controls;
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
    }

    /// <summary>系统最近授予的总宽度 (物理像素, 含 rail)。</summary>
    public int LastGrantedWidthPx { get; private set; }

    public void OnAppBarGeometry(int grantedWidthPx)
    {
        LastGrantedWidthPx = grantedWidthPx;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        LoadSvgIcons(this);
        if (DataContext is ViewModels.SidebarViewModel vm) vm.RefreshIme();
    }

    /// <summary>Tag="svg:名称" → Assets/icons/名称.svg (1.1 图标, 深色); "svgw:" → 白色变体。</summary>
    internal static void LoadSvgIcons(AvaloniaVisual root)
    {
        foreach (var image in root.GetVisualDescendants().OfType<Image>())
        {
            if (image.Tag is not string tag) continue;
            var (prefix, dir) = tag.StartsWith("svgw:") ? ("svgw:", "icons-white") : ("svg:", "icons");
            if (!tag.StartsWith(prefix)) continue;
            var name = tag[prefix.Length..];
            try
            {
                var uri = new Uri($"avares://SmartSideBAR.Avalonia/Assets/{dir}/{name}.svg");
                image.Source = new global::Avalonia.Svg.Skia.SvgImage { Source = new global::Avalonia.Svg.Skia.SvgSource(uri) };
            }
            catch
            {
                // 图标缺失不阻断 UI
            }
        }
    }
}
