// SidebarViewModel —— Wave A Demo: rail/面板切换驱动 AppBar 重排 (window:resize 通道落点雏形)。
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Windows.AppBar;

namespace SmartSideBAR.Avalonia.ViewModels;

public sealed class SidebarViewModel : INotifyPropertyChanged
{
    private readonly AppBarService _appBar;
    private bool _panelOpen;

    public SidebarViewModel(AppBarService appBar, IReadOnlyList<LinkItem> links)
    {
        _appBar = appBar;
        Links = [.. links.Where(l => l.Enabled)];
        TogglePanelCommand = new RelayCommand(_ => TogglePanel());
        OpenLinkCommand = new RelayCommand(p =>
        {
            if (p is LinkItem link) OpenLink(link);
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool PanelOpen
    {
        get => _panelOpen;
        private set
        {
            if (_panelOpen == value) return;
            _panelOpen = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PanelOpen)));
        }
    }

    public ObservableCollection<LinkItem> Links { get; }

    public string AppBarState => _appBar.IsRegistered ? "AppBar 已注册" : "Topmost 回退";

    public ICommand TogglePanelCommand { get; }

    public ICommand OpenLinkCommand { get; }

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
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.Url)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // Wave A Demo: 静默; ShellService 白名单 (§6.8) 于 Wave B 落地
        }
    }
}
