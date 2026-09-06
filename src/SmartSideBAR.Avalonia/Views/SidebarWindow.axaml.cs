using Avalonia.Controls;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Avalonia.Views;

public partial class SidebarWindow : Window
{
    private readonly AppBarEdge _side;
    private double _scaling = 1.0;

    // 供运行时 XAML 加载器可达 (AVLN3001); 应用路径一律走带 edge 的构造
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
        // K4: rail 恒为 64 物理像素; DIP 列宽按窗口 DPI 折算 (物理/96)
        _scaling = Win32Display.GetScaling(TryGetPlatformHandle()?.Handle ?? 0);
        ApplyScaling();
    }

    private void ApplyScaling()
    {
        var railDip = WindowManager.RailWidthPx / _scaling;
        RootGrid.ColumnDefinitions = _side == AppBarEdge.Right
            ? new ColumnDefinitions($"{railDip:0.#},*")
            : new ColumnDefinitions($"*,{railDip:0.#}");
    }
}
