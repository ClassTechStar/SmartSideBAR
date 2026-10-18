// SidebarViewModel —— 侧栏 rail 与面板主机。
// v1.2 面板体系：输入法(直接切换) / 截图与批注 / USB / 打印机 / 快捷链接 / 定时提醒 /
// 任务管理 / 设置。Avalonia 版此前只有"快捷链接"一个面板，其余 rail 图标点了只弹 Toast；
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

        RailItems = BuildRail();
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
        PanelKind.Reminder => "定时提醒",
        PanelKind.TaskMgr => "任务管理",
        PanelKind.Settings => "设置",
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
                // docked 时点击 rail 会先展开；展开完成后补开先前请求的面板（v1.2 350ms 语义）
                if (_pendingPanel != PanelKind.None)
                {
                    var pending = _pendingPanel;
                    _pendingPanel = PanelKind.None;
                    global::Avalonia.Threading.DispatcherTimer.RunOnce(() => OpenPanel(pending),
                        TimeSpan.FromMilliseconds(350));
                }
            }
            Raise(nameof(DockTooltip));
            Raise(nameof(DockArrowPath));
        }
    }

    public string DockTooltip => IsDocked ? "展开侧边栏" : "收起侧边栏";

    public string DockArrowPath => IsDocked
        ? "M 12 19 L 12 5 M 6 11 L 12 5 L 18 11"
        : "M 12 5 L 12 19 M 6 13 L 12 19 L 18 13";

    public ICommand ToggleImeCommand { get; }
    public ICommand ClosePanelCommand { get; }
    public ICommand DockCommand { get; }
    public ICommand OpenLinkCommand { get; }

    // ---------------------------------------------------------------- 动作
    public void OpenPanel(PanelKind kind) => OpenPanel(kind, null);

    public void OpenPanel(PanelKind kind, RailItem? source)
    {
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
        ActivePanel = PanelKind.None;
        _services.AppBar.SetExpanded(false);
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

    private void OpenLink(LinkItem link)
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
    private ObservableCollection<RailItem> BuildRail()
    {
        var disabled = _services.Config.Current.Policy.DisabledModules;
        bool On(string module) => !disabled.Contains(module, StringComparer.OrdinalIgnoreCase);

        var items = new ObservableCollection<RailItem>();

        if (On("ime"))
        {
            // 输入法图标是"动作型"：直接切换，不开面板（v1.2 railItems.ime 同语义）
            _imeItem = NewItem("ime", PanelKind.None, "输入法切换", _ => ToggleIme());
            items.Add(_imeItem);
        }
        if (On("capture"))
        {
            items.Add(NewItem("capture", PanelKind.Capture, "截图与批注", item => OpenPanel(PanelKind.Capture, item), "capture"));
            items.Add(NewItem("annotate", PanelKind.Capture, "批注", item => OpenPanel(PanelKind.Capture, item), "annotate"));
            items.Add(NewItem("longshot", PanelKind.Capture, "长截图", item => OpenPanel(PanelKind.Capture, item), "longshot"));
            items.Add(NewItem("record", PanelKind.Capture, "录屏", item => OpenPanel(PanelKind.Capture, item), "record"));
        }
        if (On("usb")) items.Add(NewItem("usb", PanelKind.Usb, "USB 监控"));
        if (On("printer")) items.Add(NewItem("printer", PanelKind.Printer, "打印机"));
        if (On("taskmgr")) items.Add(NewItem("taskmgr", PanelKind.TaskMgr, "任务管理器"));
        if (On("links")) items.Add(NewItem("link", PanelKind.Links, "快捷链接"));
        if (On("reminder")) items.Add(NewItem("bell", PanelKind.Reminder, "定时提醒"));
        if (On("settings")) items.Add(NewItem("settings", PanelKind.Settings, "设置"));

        return items;
    }

    /// <summary>
    /// 构建 rail 条目。命令闭包持有条目自身，因此"当前项高亮"按条目判定 ——
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
