using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Design;
using SmartSideBAR.Core.Diagnostics;
using SmartSideBAR.Avalonia.Ui.Panels;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Windows.AppBar;

namespace SmartSideBAR.Avalonia.Views;

public partial class SidebarWindow : Window
{
    private readonly AppBarEdge _side;
    private readonly SidebarServices _services;
    private Palette _palette;
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
        ApplyDesignTokens();
        ApplySide();

        if (services is null) return; // 设计器 / 运行时加载器路径

        DataContext = new SidebarViewModel(services, services.Config.Current.Links);
        var wasPanelOpen = false;
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SidebarViewModel.ActivePanel)) ShowPanel(Vm.ActivePanel);
            if (e.PropertyName == nameof(SidebarViewModel.PanelOpen))
            {
                // 仅在"关→开"跃迁时滑入; 面板间切换（开→开）内容直接替换, 不重播动画
                if (Vm.PanelOpen && !wasPanelOpen) AnimatePanelIn();
                wasPanelOpen = Vm.PanelOpen;
            }
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
            // 指针仍在窗口内就不收起。合成/快速移动的指针事件会误触发 PointerExited,
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

        // Esc 关闭面板 (KoBar PanelShell 语义: Esc 沿层级栈退一层)
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && Vm.PanelOpen && !Vm.IsDocked)
            {
                Vm.ClosePanel();
                e.Handled = true;
            }
        };

        // rail 长按重排序 (KoBar Sidebar 语义: 500ms 长按进入, 拖拽换位, 释放持久化 rail.order)
        AddHandler(PointerPressedEvent, OnRailPointerPressed, RoutingStrategies.Tunnel);
        AddHandler(PointerMovedEvent, OnRailPointerMoved, RoutingStrategies.Bubble);
        AddHandler(PointerReleasedEvent, OnRailPointerReleased, RoutingStrategies.Bubble);
    }

    // ---- rail 长按重排序 ----
    private global::Avalonia.Threading.DispatcherTimer? _longPressTimer;
    private IPointer? _activePointer;
    private RailItem? _dragItem;
    private bool _reordering;

    private bool IsRailButton(Visual? v, out Button? button)
    {
        button = null;
        if (v?.FindAncestorOfType<Button>(includeSelf: true) is not { } b) return false;
        if (!b.Classes.Contains("railBtn")) return false;
        // 排除 dock 钮: 只允许 RailList 内的条目参与排序
        if (b.FindAncestorOfType<ItemsControl>() != RailList) return false;
        button = b;
        return true;
    }

    private Button? FindRailButtonByItem(RailItem? item)
    {
        if (item is null) return null;
        return RailList.GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => ReferenceEquals(b.DataContext, item));
    }

    private void OnRailPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm.IsDocked || !IsRailButton(e.Source as Visual, out _)) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _dragItem = (e.Source as Visual)!.FindAncestorOfType<Button>(includeSelf: true)!.DataContext as RailItem;
        _activePointer = e.Pointer;
        _longPressTimer ??= new global::Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _longPressTimer.Stop();
        _longPressTimer.Tick += OnLongPressTick;
        _longPressTimer.Start();
    }

    private void OnLongPressTick(object? sender, EventArgs e)
    {
        _longPressTimer!.Stop();
        if (_dragItem is null || _activePointer is null) return;
        _reordering = true;
        // 窗口接管指针: 按住的按钮不再收到 Released, Click 不会误触发
        _activePointer.Capture(this);
        if (FindRailButtonByItem(_dragItem) is { } b) b.Opacity = 0.5;
    }

    private void OnRailPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_reordering || _dragItem is null) return;
        var hit = (this as global::Avalonia.Input.IInputElement)!.InputHitTest(e.GetPosition(this)) as Visual;
        if (IsRailButton(hit, out var btn) && btn!.DataContext is RailItem target && !ReferenceEquals(target, _dragItem))
        {
            var list = Vm.RailItems;
            var from = list.IndexOf(_dragItem);
            var to = list.IndexOf(target);
            if (from >= 0 && to >= 0 && from != to) list.Move(from, to);
        }
        e.Handled = true;
    }

    private void OnRailPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _longPressTimer?.Stop();
        if (!_reordering)
        {
            _dragItem = null;
            _activePointer = null;
            return;
        }
        _reordering = false;
        if (FindRailButtonByItem(_dragItem) is { } b) b.Opacity = 1;
        Vm.SaveRailOrder();   // KoBar 语义: 拖完即持久化, 重启保持
        _dragItem = null;
        _activePointer = null;
        e.Handled = true;     // 吞掉释放, 不触发点击动作
    }

    public SidebarViewModel Vm => (SidebarViewModel)DataContext!;

    /// <summary>系统最近授予的总宽度 (物理像素, 含 rail)。</summary>
    public int LastGrantedWidthPx { get; private set; }

    public void OnAppBarGeometry(int grantedWidthPx) => LastGrantedWidthPx = grantedWidthPx;

    /// <summary>配置里的主题/强调色变化后重新套用（设置面板即时生效）。</summary>
    public void ApplyTheme()
    {
        var p = AppTheme.From(_services.Config.Current.Appearance);
        _palette = p;

        // Fluent 主题跟随调色板：否则深色系统下 ComboBox/CheckBox 等标准控件按暗色绘制，
        // 落在白面板上会变成白字不可见（这也是"设置面板少了复选框"的真实原因）。
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = p.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        if (p.Glass)
        {
            // v1.2 液态玻璃: 渐变胶囊 + 顶光 + 边光环 + 高光层
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
            RailRim.BorderThickness = new Thickness(1.5);
            RailCapsule.BoxShadow = BoxShadows.Parse("0 12 40 #40000000, 0 0 0 0.5 #80FFFFFF, 0 1 3 #40FFFFFF");
            PanelHost.BoxShadow = BoxShadows.Parse("0 12 40 #40000000");
            PanelHost.BorderThickness = new Thickness(0);
            RailSheen.IsVisible = true;
            PanelSheen.IsVisible = true;
            RailCapsule.Classes.Add("glassRail");
            RailCapsule.Classes.Remove("classicRail");
        }
        else
        {
            // 1.0「经典快」: 纯白不透明 + 发丝线 + 柔和投影, 零持久模糊 (手感最好版本的表面语言)
            var classic = DesignTokens.Current.Classic;
            RailCapsule.Background = p.PanelBrush;
            RailCapsule.BoxShadow = BoxShadows.Parse($"0 0 10 {classic.Shadow}");
            RailGlowRect.Fill = null;
            RailRim.BorderBrush = p.DividerBrush;
            RailRim.BorderThickness = new Thickness(1);
            PanelHost.BoxShadow = BoxShadows.Parse($"0 0 10 {classic.Shadow}");
            PanelHost.BorderThickness = new Thickness(1);
            PanelHost.BorderBrush = p.DividerBrush;
            RailSheen.IsVisible = false;
            PanelSheen.IsVisible = false;
            RailCapsule.Classes.Add("classicRail");
            RailCapsule.Classes.Remove("glassRail");
        }

        PanelHost.Background = p.PanelBrush;
        PanelHeader.BorderBrush = p.DividerBrush;
        // v1.2 标题用强调色; 1.0 经典面标题用正文色 (App.vue --text 同源)
        PanelTitleText.Foreground = p.Glass ? p.AccentBrush : p.TextBrush;
        PanelCloseGlyph.Foreground = p.Glass ? p.AccentBrush : p.MutedBrush;

        Resources["RailHover"] = p.Glass
            ? (IBrush)new SolidColorBrush(p.Dark
                ? Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x14, 0x0F, 0x17, 0x2A))
            : new SolidColorBrush(ParseHex(DesignTokens.Current.Classic.BrandLight, "#E8F1FC"));
        Resources["RailPressed"] = p.Glass
            ? (IBrush)new SolidColorBrush(p.Dark
                ? Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x21, 0x0F, 0x17, 0x2A))
            : new SolidColorBrush(ParseHex(DesignTokens.Current.Classic.PressedHover, "#D9E8FB"));
        Resources["RailActive"] = new SolidColorBrush(p.Glass
            ? p.AccentSoft
            : ParseHex(DesignTokens.Current.Classic.BrandLight, "#E8F1FC"));
        // v1.2 .rail-badge: 品牌色底 + 白字（白底深字在玻璃胶囊上几乎看不见）
        Resources["BadgeBg"] = new SolidColorBrush(p.Accent);
        Resources["BadgeFg"] = new SolidColorBrush(Colors.White);
        // KoBar 选中态指示条: 品牌色竖条
        Resources["IndicatorBar"] = new SolidColorBrush(p.Accent);

        // 深色主题下 rail 图标需要白色变体
        if (DataContext is SidebarViewModel vm)
        {
            foreach (var item in vm.RailItems) item.SetIconDark(p.Dark);
            vm.SetDockIconDark(p.Dark);
        }

        _railSheen?.Apply(p);
        _panelSheen?.Apply(p);
        // 面板在构造时持有调色板副本, 用新调色板重建当前面板, 主题切换立即生效
        if (DataContext is SidebarViewModel vm2 && vm2.PanelOpen) ShowPanel(vm2.ActivePanel);
    }

    private void OnSheenPointerMoved(object? sender, PointerEventArgs e)
    {
        // 经典面无高光层（1.0 同源），跳过跟随计算
        if (!_palette.Glass) return;
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

    /// <summary>
    /// 把 design-tokens.json 的几何规格落到窗口（P0-2）。
    /// XAML 里仍写着一份默认值以便设计器预览, 运行时一律以令牌为准,
    /// 这样停靠箭头这类"反复微调"的参数就只有一处可改。
    /// </summary>
    private void ApplyDesignTokens()
    {
        var tokens = DesignTokens.Current;

        PanelHost.Width = tokens.Panel.WidthDip;
        PanelHost.CornerRadius = new global::Avalonia.CornerRadius(tokens.Panel.Radius);

        RailCapsule.CornerRadius = new global::Avalonia.CornerRadius(tokens.Capsule.Radius);

        if (DockArrowIcon is not null)
        {
            DockArrowIcon.Width = tokens.Dock.ArrowSizePx;
            DockArrowIcon.Height = tokens.Dock.ArrowSizePx;
        }
    }

    private void ShowPanel(PanelKind kind)
    {
        _panel = kind switch
        {
            PanelKind.Capture => new CapturePanel(_services, _palette),
            PanelKind.Usb => new UsbPanel(_services, _palette),
            PanelKind.Printer => new PrinterPanel(_services, _palette),
            PanelKind.Links => new LinksPanel(_services, _palette, link => Vm.OpenLink(link)),
            PanelKind.Reminder => new ReminderPanel(_services, _palette),
            PanelKind.TaskMgr => new TaskMgrPanel(_services, _palette),
            PanelKind.Settings => new SettingsPanel(_services, _palette),
            PanelKind.QuickSettings => new QuickSettingsPanel(_services, _palette),
            _ => null,
        };
        PanelBody.Content = _panel;
        PanelScroll.Offset = default;
        _panel?.Refresh();

        // P2-3：崩溃上下文记录"崩在哪个面板"
        CrashLog.LastContext = CrashLog.LastContext is { } ctx
            ? ctx with { ActivePanel = kind.ToString() }
            : new CrashContext("unknown", 1.0, kind.ToString(), SafeMode.OsBuildString());
    }

    /// <summary>
    /// 面板滑入动效（1.0 手感基准: width 0→380 @180ms ease + 内容淡入）。
    /// 窗口宽度由 AppBar 授予 (瞬时变化), 动效只作用于窗口内的 PanelHost,
    /// 打开时窗口先变宽、面板再从 rail 侧向外滑出（动画期锚点临时切到 rail 侧）。
    /// reduceMotion 或安全模式下直接落位。
    /// </summary>
    private void AnimatePanelIn()
    {
        var tokens = DesignTokens.Current;
        if (_services.Config.Current.Appearance.ReduceMotion || SafeMode.FromEnvironment())
        {
            PanelHost.Width = tokens.Panel.WidthDip;
            PanelBody.Opacity = 1;
            return;
        }

        // 锚在 rail 侧生长（右侧栏=Right 对齐）, 动画结束后恢复 ApplySide 的语义对齐
        var original = PanelHost.HorizontalAlignment;
        PanelHost.HorizontalAlignment = _side == AppBarEdge.Left
            ? global::Avalonia.Layout.HorizontalAlignment.Left
            : global::Avalonia.Layout.HorizontalAlignment.Right;
        PanelHost.Width = 0;
        PanelBody.Opacity = 0;

        var width = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(tokens.Motion.PanelMs),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(WidthProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(WidthProperty, tokens.Panel.WidthDip) } },
            },
        };
        _ = width.RunAsync(PanelHost);

        var fade = new Animation
        {
            Duration = TimeSpan.FromMilliseconds(Math.Max(120, tokens.Motion.PanelMs * 0.8)),
            Easing = new CubicEaseOut(),
            FillMode = FillMode.Forward,
            Children =
            {
                new KeyFrame { Cue = new Cue(0), Setters = { new Setter(OpacityProperty, 0d) } },
                new KeyFrame { Cue = new Cue(1), Setters = { new Setter(OpacityProperty, 1d) } },
            },
        };
        _ = fade.RunAsync(PanelBody);

        // 落定本地值, 不依赖 FillMode 的语义差异; 同时恢复语义对齐
        DispatcherTimer.RunOnce(() =>
        {
            PanelHost.Width = tokens.Panel.WidthDip;
            PanelBody.Opacity = 1;
            PanelHost.HorizontalAlignment = original;
        }, TimeSpan.FromMilliseconds(tokens.Motion.PanelMs + 50));
    }

    private static Color ParseHex(string hex, string fallback)
    {
        try { return Color.Parse(hex); }
        catch { return Color.Parse(fallback); }
    }

    /// <summary>
    /// B16  XAML 里 rail 固定 Right / 面板固定 Left, 侧栏配到左侧时展开态会把 rail 甩到
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
            // 选中指示条贴屏侧跟随停靠边 (KoBar BarItem 语义)
            rail.Classes.Add(_side == AppBarEdge.Left ? "sideLeft" : "sideRight");
            rail.Classes.Remove(_side == AppBarEdge.Left ? "sideRight" : "sideLeft");
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
