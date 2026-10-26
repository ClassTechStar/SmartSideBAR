// SidebarViewModel: 侧栏 rail 与面板主机。
// v1.2 面板体系：输入法(直接切换) / 截图与批注 / USB / 打印机 / 快捷链接 / 定时提醒 /
// 任务管理 / 设置。缺面板时 rail 图标点了只弹 Toast,
// 本文件把 rail 与面板全部数据化，并按 policy.disabledModules 过滤。
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.ViewModels;

public sealed class SidebarViewModel : INotifyPropertyChanged
{
    private readonly SidebarServices _services;
    private PanelKind _activePanel = PanelKind.None;
    private RailItem? _activeRail;
    private string _imeState = "中";
    private RailItem? _imeItem;
    private PanelKind _pendingPanel = PanelKind.None;
    private int _panelEpoch;

    public SidebarViewModel(SidebarServices services, IReadOnlyList<LinkItem> links)
    {
        _services = services;
        Links = [.. links.Where(l => l.Enabled).Select(l => new LinkRow(l, row => OpenLink(row.Source)))];

        ToggleImeCommand = new RelayCommand(_ =>
        {
            _ = _services.Ime.Toggle();
            RefreshIme();
        });
        ClosePanelCommand = new RelayCommand(_ => ClosePanel());
        DockCommand = new RelayCommand(_ => _services.Dispatch("dock"));
        OpenLinkCommand = new RelayCommand(p =>
        {
            if (p is LinkItem link) OpenLink(link);
        });

        RailItems = [];
        PopulateRail();
        RefreshIme();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<RailItem> RailItems { get; }

    public ObservableCollection<LinkRow> Links { get; }

    public SidebarServices Services => _services;

    // ---------------------------------------------------------------- 面板状态
    public PanelKind ActivePanel
    {
        get => _activePanel;
        private set
        {
            if (_activePanel == value) return;
            _activePanel = value;
            if (value == PanelKind.None) _activeRail = null;
            foreach (var item in RailItems) item.IsActive = ReferenceEquals(item, _activeRail);
            Raise(nameof(ActivePanel));
            Raise(nameof(PanelOpen));
            Raise(nameof(PanelTitle));
        }
    }

    public bool PanelOpen => _activePanel != PanelKind.None;

    public string PanelTitle => _activePanel switch
    {
        PanelKind.Capture => "截图与批注",
        PanelKind.Usb => "USB 设备",
        PanelKind.Printer => "打印机",
        PanelKind.Links => "快捷链接",
        PanelKind.Reminder => "提醒与通知",
        PanelKind.TaskMgr => "任务管理",
        PanelKind.Settings => "设置",
        PanelKind.QuickSettings => "快捷设置",
        _ => "",
    };

    public string ImeState
    {
        get => _imeState;
        private set
        {
            if (_imeState == value) return;
            _imeState = value;
            Raise(nameof(ImeState));
        }
    }

    private bool _isDocked;

    /// <summary>v1.1 语义: docked = 收起为右下角 52×52 小方块。</summary>
    public bool IsDocked
    {
        get => _isDocked;
        set
        {
            if (_isDocked == value) return;
            _isDocked = value;
            if (value) Raise(nameof(IsDocked));
            if (value)
            {
                ClosePanel();
            }
            else
            {
                Raise(nameof(IsDocked));
                // docked 时点击 rail 会先展开; 展开完成后补开刚才请求的面板 (v1.2 350ms 语义)
                if (_pendingPanel != PanelKind.None)
                {
                    var pending = _pendingPanel;
                    _pendingPanel = PanelKind.None;
                    global::Avalonia.Threading.DispatcherTimer.RunOnce(() => OpenPanel(pending),
                        TimeSpan.FromMilliseconds(350));
                }
            }
            Raise(nameof(DockTooltip));
            Raise(nameof(DockIcon));
        }
    }

    public string DockTooltip => IsDocked ? "展开侧边栏" : "收起侧边栏";

    /// <summary>
    /// 停靠箭头用图标而不是 Path 字符串: <c>Path Data</c> 配 Stretch=Uniform 会把
    /// 12×14 的几何拉伸到 24×24，箭头既被拉扁又偏离圆心（用户截图里的"箭头位置有点偏"）。
    /// </summary>
    public global::Avalonia.Media.IImage? DockIcon => Ui.SvgIcons.Load(IsDocked ? "dock-up" : "dock-down", _dockIconWhite);

    private bool _dockIconWhite;

    /// <summary>深色主题下换成白色描边图标。</summary>
    public void SetDockIconDark(bool dark)
    {
        if (_dockIconWhite == dark) return;
        _dockIconWhite = dark;
        Raise(nameof(DockIcon));
    }

    public ICommand ToggleImeCommand { get; }
    public ICommand ClosePanelCommand { get; }
    public ICommand DockCommand { get; }
    public ICommand OpenLinkCommand { get; }

    // ---------------------------------------------------------------- 动作
    public void OpenPanel(PanelKind kind) => OpenPanel(kind, null);

    public void OpenPanel(PanelKind kind, RailItem? source)
    {
        _panelEpoch++;   // 作废所有在途的关闭回调
        if (source is not null) _activeRail = source;
        if (_isDocked)
        {
            // docked 时先展开侧栏再打开面板（v1.2 同语义）
            _pendingPanel = kind;
            _services.Dispatch("dock");
            return;
        }
        ActivePanel = kind;
        _services.AppBar.SetExpanded(true);
    }

    public void ClosePanel()
    {
        if (_activePanel == PanelKind.None) return;

        // 顺序很关键：先缩窗口（面板内容仍在，只是被裁剪），再卸载内容树。
        // 反过来做（先 Content=null 再缩窗口）会在"销毁内容 + 改 Win32 尺寸"的同一帧里
        // 与 shell/DWM 的布局回调打交叉，偶发原生崩溃（无托管异常、无 crash.log）。
        var epoch = ++_panelEpoch;
        _services.AppBar.SetExpanded(false);
        global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // 期间用户又点了别的面板 → 本次关闭作废（否则会把新面板顶掉，
            // 并留下"窗口在展开位置却只有 rail 宽"的错位状态）
            if (epoch == _panelEpoch) ActivePanel = PanelKind.None;
        }, global::Avalonia.Threading.DispatcherPriority.Background);
    }

    public void ToggleIme()
    {
        _ = _services.Ime.Toggle();
        RefreshIme();
    }

    public void RefreshIme()
    {
        var state = _services.Ime.GetState();
        ImeState = state.IsChinese ? "中" : "EN";
        if (_imeItem is not null) _imeItem.Badge = ImeState;
    }

    public void OpenLink(LinkItem link)
    {
        try
        {
            _services.Shell.OpenExternal(new Uri(link.Url));
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"无法打开链接「{link.Name}」: {ex.Message}", "error");
        }
    }

    // ---------------------------------------------------------------- rail 构建

    /// <summary>
    /// 当前工作区允许的模块集合（KoBar 工作区语义）。
    /// null = 当前预设不限制 (Modules 为空或无预设), 只受 policy.disabledModules 约束。
    /// </summary>
    private IReadOnlySet<string>? WorkspaceAllowed()
    {
        var ws = _services.Config.Current.Workspace;
        var preset = ws.Presets.FirstOrDefault(p => string.Equals(p.Name, ws.Current, StringComparison.OrdinalIgnoreCase));
        if (preset is null || preset.Modules.Count == 0) return null;
        return new HashSet<string>(preset.Modules, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>rail 顺序：config.rail.order 优先，未登记模块按内置顺序追加在末尾。</summary>
    private int OrderOf(string module)
    {
        var order = _services.Config.Current.Rail.Order;
        var idx = order.FindIndex(m => string.Equals(m, module, StringComparison.OrdinalIgnoreCase));
        return idx >= 0 ? idx : order.Count + 100;
    }

    /// <summary>按当前配置（工作区 + 策略 + 顺序）重建 rail。工作区切换后调用。</summary>
    public void RebuildRail()
    {
        RailItems.Clear();
        PopulateRail();
        RefreshIme();
        // 工作区切走后原激活项可能已被过滤, 保持面板打开但取消高亮即可
        if (_activeRail is not null && !RailItems.Contains(_activeRail))
        {
            _activeRail = null;
            foreach (var item in RailItems) item.IsActive = false;
        }
    }

    private void PopulateRail()
    {
        var disabled = _services.Config.Current.Policy.DisabledModules;
        var allowed = WorkspaceAllowed();
        bool On(string module) =>
            !disabled.Contains(module, StringComparer.OrdinalIgnoreCase)
            && (allowed is null || allowed.Contains(module));

        var items = new List<RailItem>();

        if (On("ime"))
        {
            // 输入法图标是"动作型"：直接切换，不开面板（v1.2 railItems.ime 同语义）
            _imeItem = NewItem("ime", PanelKind.None, "输入法切换", _ => ToggleIme());
            items.Add(_imeItem);
        }
        if (On("capture"))
        {
            // 与 v1.2 一致：rail 图标只负责打开"截图与批注"面板，具体动作在面板磁贴里执行。
            // (图标若同时触发动作, 会立刻盖一层全屏覆盖层, 把刚打开的面板压在下面。)
            items.Add(NewItem("capture", PanelKind.Capture, "截图与批注"));
        }
        // 逐项 gate（工作区/策略可独立裁剪单个能力, 而非整组):
        // 「批注」工作区只放行 ime+annotate 时, record/longshot 消失而 annotate 保留。
        if (On("annotate")) items.Add(NewItem("annotate", PanelKind.Capture, "批注"));
        if (On("longshot")) items.Add(NewItem("longshot", PanelKind.Capture, "长截图"));
        if (On("record")) items.Add(NewItem("record", PanelKind.Capture, "录屏"));
        if (On("usb")) items.Add(NewItem("usb", PanelKind.Usb, "USB 监控"));
        if (On("printer")) items.Add(NewItem("printer", PanelKind.Printer, "打印机"));
        if (On("taskmgr")) items.Add(NewItem("taskmgr", PanelKind.TaskMgr, "任务管理器"));
        if (On("links")) items.Add(NewItem("link", PanelKind.Links, "快捷链接"));
        if (On("bell")) items.Add(NewItem("bell", PanelKind.Reminder, "提醒与通知"));
        if (On("settings")) items.Add(NewItem("settings", PanelKind.QuickSettings, "快捷设置"));

        // rail.order 持久化顺序（长按重排序写入）；未登记的保持内置相对顺序
        foreach (var item in items.OrderBy(i => OrderOf(i.Module))) RailItems.Add(item);
    }

    /// <summary>重排序结束后回写 rail.order（SidebarWindow 拖拽逻辑调用）。</summary>
    public void SaveRailOrder()
    {
        _ = _services.Config.Set("rail.order", RailItems.Select(i => i.Module).ToList());
    }

    /// <summary>
    /// 构建 rail 条目。命令闭包持有条目自身, "当前项高亮"按条目判定,
    /// 截图组有 4 个条目共享同一面板，不能因为面板相同就同时点亮。
    /// </summary>
    private RailItem NewItem(string icon, PanelKind kind, string title,
        Action<RailItem>? activate = null, string? action = null)
    {
        RailItem item = null!;
        item = new RailItem
        {
            Module = icon,
            Kind = kind,
            Title = title,
            IconName = icon,
            Command = new RelayCommand(_ =>
            {
                // 先点亮本条，再切面板/触发动作（与 v1.2 activePanel + action 的顺序一致）
                _activeRail = item;
                foreach (var other in RailItems) other.IsActive = ReferenceEquals(other, item);

                if (activate is not null) activate(item);
                else OpenPanel(kind, item);

                if (action is not null) _services.Dispatch(action);
            }),
        };
        return item;
    }

    private void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
