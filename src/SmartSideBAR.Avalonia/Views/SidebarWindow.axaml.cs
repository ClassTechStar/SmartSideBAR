using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Avalonia.Ui.Panels;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Windows.AppBar;

namespace SmartSideBAR.Avalonia.Views;

public partial class SidebarWindow : Window
{
    private readonly AppBarEdge _side;
    private readonly SidebarServices _services;
    private readonly Palette _palette;
    private readonly DispatcherTimer _collapseTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private SidebarPanel? _panel;
    private GlassSheen? _railSheen;
    private GlassSheen? _panelSheen;

    // 供运行时 XAML 加载器可达; 应用路径一律走带 edge 的构造
    public SidebarWindow() : this(AppBarEdge.Right, null!) { }

    public SidebarWindow(AppBarEdge side, SidebarServices services)
    {
        _side = side;
        _services = services;
        _palette = AppTheme.From(services?.Config.Current.Appearance);

        InitializeComponent();
        ApplySide();

        if (services is null) return; // 设计器 / 运行时加载器路径

        DataContext = new SidebarViewModel(services, services.Config.Current.Links);
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SidebarViewModel.ActivePanel)) ShowPanel(Vm.ActivePanel);
        };

        ApplyTheme();

        // 液态玻璃高光：镜面光跟随光标（v1.2 --lg-mx/--lg-my）+ 色散边（v1.2 aberration）
        var appearance = services.Config.Current.Appearance;
        _railSheen = new GlassSheen(RailSheen, _palette, 28, appearance.Aberration, appearance.Specular);
        _panelSheen = new GlassSheen(PanelSheen, _palette, 28, appearance.Aberration, appearance.Specular);
        AddHandler(PointerMovedEvent, OnSheenPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(PointerExitedEvent, (_, _) =>
        {
            _railSheen?.Track(0, 0);
            _panelSheen?.Track(0, 0);
        }, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);

        _collapseTimer.Tick += (_, _) =>
        {
            _collapseTimer.Stop();
            // 指针仍在窗口内就不收起 —— 合成/快速移动的指针事件会误触发 PointerExited，
            // 只靠离开事件判断会把正在操作的面板关掉。
            if (IsPointerOver)
            {
                _collapseTimer.Start();
                return;
            }
            // 截图/批注覆盖层在场时不收起（v1.2 __keepSidebarOpen 同语义），稍后重试
            if (OverlayGate.AnyOpen)
            {
                if (Vm.PanelOpen) _collapseTimer.Start();
                return;
            }
            // 鼠标离开 5 s 自动收起（v1.2 SidebarApp.onMouseLeave 同语义）
            if (!Vm.IsDocked && Vm.PanelOpen) Vm.ClosePanel();
        };
        PointerEntered += (_, _) => _collapseTimer.Stop();
        PointerExited += (_, _) =>
        {
            if (Vm.PanelOpen && !Vm.IsDocked) _collapseTimer.Start();
        };

        // 截图/批注结束后刷新当前面板（最近文件列表随之更新）
        OverlayGate.AllClosed += () => global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (Vm.PanelOpen) _panel?.Refresh();
        });

        // 双击 rail 空白区切换 dock（v1.2 onRailDblClick）
        RailCapsule.DoubleTapped += (_, e) =>
        {
            if (e.Source is Visual v && v.FindAncestorOfType<Button>(includeSelf: true) is not null) return;
            _services.Dispatch("dock");
        };
    }

    public SidebarViewModel Vm => (SidebarViewModel)DataContext!;

    /// <summary>系统最近授予的总宽度 (物理像素, 含 rail)。</summary>
    public int LastGrantedWidthPx { get; private set; }

    public void OnAppBarGeometry(int grantedWidthPx) => LastGrantedWidthPx = grantedWidthPx;

    /// <summary>配置里的主题/强调色变化后重新套用（设置面板即时生效）。</summary>
    public void ApplyTheme()
    {
        var p = AppTheme.From(_services.Config.Current.Appearance);

        // Fluent 主题跟随调色板：否则深色系统下 ComboBox/CheckBox 等标准控件按暗色绘制，
        // 落在白面板上会变成白字不可见（这也是"设置面板少了复选框"的真实原因）。
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = p.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        RailCapsule.Background = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(p.RailFrom, 0),
                new GradientStop(p.RailTo, 1),
            },
        };

        RailGlowRect.Fill = new RadialGradientBrush
        {
            Center = new RelativePoint(0.18, 0, RelativeUnit.Relative),
            RadiusX = new RelativeScalar(1.2, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(0.9, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(p.RailGlow, 0),
                new GradientStop(Color.FromArgb(0x24, p.RailGlow.R, p.RailGlow.G, p.RailGlow.B), 0.42),
                new GradientStop(Color.FromArgb(0, p.RailGlow.R, p.RailGlow.G, p.RailGlow.B), 0.68),
            },
        };

        RailRim.BorderBrush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(p.RimFrom, 0),
                new GradientStop(Color.FromArgb(0x47, 0xFF, 0xFF, 0xFF), 0.18),
                new GradientStop(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF), 0.4),
                new GradientStop(Color.FromArgb(0x29, 0xFF, 0xFF, 0xFF), 0.62),
                new GradientStop(p.RimTo, 1),
            },
        };

        PanelHost.Background = p.PanelBrush;
        PanelHeader.BorderBrush = p.DividerBrush;
        PanelTitleText.Foreground = p.AccentBrush;
        PanelCloseGlyph.Foreground = p.AccentBrush;

        if (DockBtn.Content is global::Avalonia.Controls.Shapes.Path arrow)
        {
            arrow.Stroke = new SolidColorBrush(p.Dark ? Color.Parse("#E8EDF5") : Color.Parse("#333333"));
        }

        Resources["RailHover"] = new SolidColorBrush(p.Dark
            ? Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(0x14, 0x0F, 0x17, 0x2A));
        Resources["RailPressed"] = new SolidColorBrush(p.Dark
            ? Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)
            : Color.FromArgb(0x21, 0x0F, 0x17, 0x2A));
        Resources["RailActive"] = new SolidColorBrush(p.AccentSoft);
        Resources["BadgeBg"] = new SolidColorBrush(p.Dark
            ? Color.FromArgb(0xD1, 0x2A, 0x30, 0x3C)
            : Color.FromArgb(0xD1, 0xFF, 0xFF, 0xFF));
        Resources["BadgeFg"] = new SolidColorBrush(p.Dark
            ? Color.Parse("#E8EDF5")
            : Color.Parse("#2A2F38"));

        // 深色主题下 rail 图标需要白色变体
        if (DataContext is SidebarViewModel vm)
        {
            foreach (var item in vm.RailItems) item.SetIconDark(p.Dark);
        }

        _railSheen?.Apply(p);
        _panelSheen?.Apply(p);
        _panel?.Refresh();
    }

    private void OnSheenPointerMoved(object? sender, PointerEventArgs e)
    {
        Track(RailCapsule, _railSheen, e);
        if (Vm.PanelOpen) Track(PanelHost, _panelSheen, e);
    }

    private static void Track(Control host, GlassSheen? sheen, PointerEventArgs e)
    {
        if (sheen is null) return;
        var bounds = host.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var local = e.GetPosition(host);
        sheen.Track(local.X / bounds.Width * 2 - 1, local.Y / bounds.Height * 2 - 1);
    }

    private void ShowPanel(PanelKind kind)
    {
        _panel = kind switch
        {
            PanelKind.Capture => new CapturePanel(_services, _palette),
            PanelKind.Usb => new UsbPanel(_services, _palette),
            PanelKind.Printer => new PrinterPanel(_services, _palette),
            PanelKind.Links => new LinksPanel(_services, _palette, Vm.Links),
            PanelKind.Reminder => new ReminderPanel(_services, _palette),
            PanelKind.TaskMgr => new TaskMgrPanel(_services, _palette),
            PanelKind.Settings => new SettingsPanel(_services, _palette),
            _ => null,
        };
        PanelBody.Content = _panel;
        PanelScroll.Offset = default;
        _panel?.Refresh();
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
                ? global::Avalonia.Layout.HorizontalAlignment.Left
                : global::Avalonia.Layout.HorizontalAlignment.Right;
        }
        if (panel is not null)
        {
            panel.HorizontalAlignment = _side == AppBarEdge.Left
                ? global::Avalonia.Layout.HorizontalAlignment.Right
                : global::Avalonia.Layout.HorizontalAlignment.Left;
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
    internal static void LoadSvgIcons(Visual root)
    {
        if (!root.IsAttachedToVisualTree()) return;
        foreach (var image in root.GetVisualDescendants().OfType<Image>())
        {
            if (image.Tag is not string tag) continue;
            var (prefix, white) = tag.StartsWith("svgw:") ? ("svgw:", true) : ("svg:", false);
            if (!tag.StartsWith(prefix)) continue;
            var source = SvgIcons.Load(tag[prefix.Length..], white);
            if (source is not null) image.Source = source;
        }
    }
}
