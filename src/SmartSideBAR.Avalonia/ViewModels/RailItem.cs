// rail 条目视图模型 —— v1.2 railItems 的数据化对应物。
// 把 rail 从 XAML 里 11 个硬编码按钮改成数据驱动后，可以同时获得：
// 当前项高亮(IsActive)、IME 角标(Badge)、策略禁用过滤(在 SidebarViewModel 里按 policy 过滤)。
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Media;

namespace SmartSideBAR.Avalonia.ViewModels;

public enum PanelKind
{
    None,
    Capture,
    Usb,
    Printer,
    Links,
    Reminder,
    TaskMgr,
    Settings,
}

public sealed class RailItem : INotifyPropertyChanged
{
    private bool _isActive;
    private string? _badge;

    public required string Module { get; init; }
    public required PanelKind Kind { get; init; }
    public required string Title { get; init; }
    private IImage? _icon;
    private string? _iconName;

    public IImage? Icon
    {
        get => _icon;
        private set
        {
            _icon = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Icon)));
        }
    }

    /// <summary>切换深/浅主题时重建图标（深色底需要白色线条）。</summary>
    public void SetIconDark(bool dark)
    {
        if (_iconName is null) return;
        Icon = Ui.SvgIcons.Load(_iconName, white: dark);
    }

    public string? IconName
    {
        get => _iconName;
        init
        {
            _iconName = value;
            _icon = value is null ? null : Ui.SvgIcons.Load(value);
        }
    }

    public ICommand? Command { get; init; }

    public string? Badge
    {
        get => _badge;
        set
        {
            if (_badge == value) return;
            _badge = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Badge)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasBadge)));
        }
    }

    public bool HasBadge => !string.IsNullOrEmpty(_badge);

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value) return;
            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
