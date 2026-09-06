// SidebarViewModel —— 侧栏 rail/面板: AppBar 切换 + IME + 任务管理器 + 链接。
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Input;
using SmartSideBAR.Windows.Shell;

namespace SmartSideBAR.Avalonia.ViewModels;

public sealed class SidebarViewModel : INotifyPropertyChanged
{
    private readonly AppBarService _appBar;
    private readonly IImeService _ime;
    private readonly IShellService _shell;
    private bool _panelOpen;
    private string _imeState = "IME";

    public SidebarViewModel(AppBarService appBar, IImeService ime, IShellService shell, IReadOnlyList<LinkItem> links, Action<string> dispatch)
    {
        _appBar = appBar;
        _ime = ime;
        _shell = shell;
        Dispatch = dispatch;
        Links = [.. links.Where(l => l.Enabled)];
        TogglePanelCommand = new RelayCommand(_ => TogglePanel());
        OpenLinkCommand = new RelayCommand(p =>
        {
            if (p is LinkItem link) OpenLink(link);
        });
        ToggleImeCommand = new RelayCommand(_ =>
        {
            _ = _ime.Toggle();
            RefreshIme();
        });
        OpenTaskMgrCommand = new RelayCommand(_ => _shell.OpenTaskManager());
        CaptureCommand = new RelayCommand(_ => Dispatch("capture"));
        AnnotateCommand = new RelayCommand(_ => Dispatch("annotate"));
        RecordCommand = new RelayCommand(_ => Dispatch("record"));
        LongshotCommand = new RelayCommand(_ => Dispatch("longshot"));
        UsbCommand = new RelayCommand(_ => Dispatch("usb"));
        PrinterCommand = new RelayCommand(_ => Dispatch("printer"));
        SettingsCommand = new RelayCommand(_ => Dispatch("settings"));
        ReminderCommand = new RelayCommand(_ => Dispatch("reminder"));
        DockCommand = new RelayCommand(_ => Dispatch("dock"));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool PanelOpen
    {
        get => _panelOpen;
        set
        {
            if (_panelOpen == value) return;
            _panelOpen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelOpen)));
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDocked)));
            if (value) PanelOpen = false;
        }
    }

    public string ImeState
    {
        get => _imeState;
        private set
        {
            if (_imeState == value) return;
            _imeState = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ImeState)));
        }
    }

    public ObservableCollection<LinkItem> Links { get; }

    public string AppBarState => _appBar.IsRegistered ? "AppBar 已注册" : "Topmost 回退";

    public Action<string> Dispatch { get; }
    public ICommand TogglePanelCommand { get; }
    public ICommand OpenLinkCommand { get; }
    public ICommand ToggleImeCommand { get; }
    public ICommand OpenTaskMgrCommand { get; }
    public ICommand CaptureCommand { get; private set; } = new RelayCommand(_ => { });
    public ICommand AnnotateCommand { get; private set; } = new RelayCommand(_ => { });
    public ICommand RecordCommand { get; private set; } = new RelayCommand(_ => { });
    public ICommand LongshotCommand { get; private set; } = new RelayCommand(_ => { });
    public ICommand UsbCommand { get; private set; } = new RelayCommand(_ => { });
    public ICommand PrinterCommand { get; private set; } = new RelayCommand(_ => { });
    public ICommand SettingsCommand { get; private set; } = new RelayCommand(_ => { });
    public ICommand ReminderCommand { get; private set; } = new RelayCommand(_ => { });
    public ICommand DockCommand { get; private set; } = new RelayCommand(_ => { });

    public void RefreshIme()
    {
        var s = _ime.GetState();
        ImeState = s.IsChinese ? "中" : "EN";
    }

    /// <summary>展开/收起: 窗口宽度变化经 AppBarService 与系统协商 (物理像素)。</summary>
    public void TogglePanel()
    {
        PanelOpen = !PanelOpen;
        _appBar.SetExpanded(PanelOpen);
    }

    public void OpenLink(LinkItem link)
    {
        try
        {
            _shell.OpenExternal(new Uri(link.Url));
        }
        catch (Exception)
        {
            // 白名单外域名拒绝 (B2); UI 提示于 Wave C 通知系统
        }
    }
}
