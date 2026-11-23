// M3: U 盘面板 VM（监控状态徽标 + 可移动磁盘列表）。
using System.Collections.ObjectModel;
using Avalonia.Media;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Windows.Devices;

namespace SmartSideBAR.Avalonia.Ui.Panels.ViewModels;

internal sealed record UsbRow(
    string Drive, string Label, string SizeText, IImage? Icon, RelayCommand OpenCommand);

internal sealed class UsbPanelViewModel : PanelViewModel
{
    public UsbPanelViewModel(SidebarServices services, Palette palette) : base(services, palette)
    {
        RefreshCommand = new RelayCommand(_ => Reload());
    }

    public bool Running { get; private set; }

    public string BadgeText => Running ? "运行中" : "已停止";

    public IBrush BadgeForeground => Brush(Running ? AppTheme.Ok : Palette.Muted);

    public IBrush BadgeBackground => Running
        ? Brush(Color.FromArgb(0x1F, AppTheme.Ok.R, AppTheme.Ok.G, AppTheme.Ok.B))
        : Brush(Palette.Hover);

    public ObservableCollection<UsbRow> Drives { get; } = [];

    public bool IsEmpty { get; private set; } = true;

    public string CountText => $"已连接设备（{Drives.Count}）";

    public IImage? EmptyIcon => SvgIcons.Load("usb", Palette.Dark);

    public RelayCommand RefreshCommand { get; }

    public override void Reload()
    {
        Running = Services.Config.Current.Usb.Enabled;
        Raise(nameof(Running));
        Raise(nameof(BadgeText));
        Raise(nameof(BadgeForeground));
        Raise(nameof(BadgeBackground));

        IReadOnlyList<UsbDriveInfo> drives;
        try { drives = Services.Usb.ListRemovable(); }
        catch { drives = []; }

        var icon = SvgIcons.Load("usb", Palette.Dark);
        Drives.Clear();
        foreach (var d in drives)
        {
            // 空读卡器/无介质时 SizeGb 为 0，不要显示成 "0 GB"（看起来像故障）
            var capacity = d.SizeGb > 0 ? $"{d.SizeGb:0.#} GB" : "容量未知";
            var path = d.Drive;
            Drives.Add(new UsbRow(
                d.Drive,
                string.IsNullOrWhiteSpace(d.Label) ? "USB 存储设备" : d.Label,
                $"{capacity} · {(d.Removable ? "可移动磁盘" : "固定磁盘")}",
                icon,
                new RelayCommand(_ =>
                {
                    try { Services.Shell.OpenPath(path); }
                    catch (Exception ex) { ToastService.Instance.Show($"打开 {path} 失败: {ex.Message}", "error"); }
                })));
        }

        IsEmpty = Drives.Count == 0;
        Raise(nameof(IsEmpty));
        Raise(nameof(CountText));
        Raise(nameof(EmptyIcon));
    }
}
