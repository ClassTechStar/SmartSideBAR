// ReminderPanel 的 code-behind（M3: 内容移至 ReminderPanel.axaml）。
// 这里保留两件必须依赖可视树的工作: 通知中心变更订阅（随控件挂载/卸载）与 MP3 文件选择（需要 TopLevel）。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Transformation;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using SmartSideBAR.Avalonia.Ui.Panels.ViewModels;
using SmartSideBAR.Avalonia.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed partial class ReminderPanel : SidebarPanel
{
    private bool _hooked;
    private Action? _onNotificationsChanged;

    public ReminderPanel(SidebarServices services, Palette palette) : base(services, palette)
    {
        PanelTheme.Apply(Resources, palette);
        InitializeComponent();
        Vm = new ReminderPanelViewModel(services, palette) { RefreshRequested = Refresh };
        DataContext = Vm;
    }

    internal ReminderPanelViewModel Vm { get; }

    protected override void RefreshCore()
    {
        Vm.Reload();
        OnBuilt();
    }

    protected override void OnBuilt()
    {
        // P2-3: 页签切换后播放横向入场动画；关闭动效时直接显示
        if (Vm.EnterDirection != 0 && Bounds.Width > 0)
        {
            var animate = !Services.Config.Current.Appearance.ReduceMotion;
            _ = PageTransition.SlideInAsync(this, Bounds.Width, Vm.EnterDirection, animate);
        }

        // 通知有更新时刷新通知页签（只挂一次; 面板脱离可视树时摘除）
        if (_hooked) return;
        _hooked = true;
        _onNotificationsChanged = () => Dispatcher.UIThread.Post(Refresh);
        NotificationCenter.Instance.Changed += _onNotificationsChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_onNotificationsChanged is not null)
        {
            NotificationCenter.Instance.Changed -= _onNotificationsChanged;
            _hooked = false;
        }
    }

    private async void OnPickMp3Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            var top = TopLevel.GetTopLevel(this);
            if (top?.StorageProvider is not { } provider) return;
            var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择提醒铃声",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("音频文件")
                    {
                        Patterns = ["*.mp3", "*.wav", "*.m4a", "*.wma"],
                    },
                ],
            });
            if (files.Count == 0) return;
            var path = files[0].TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;
            Vm.SetMp3Path(path);
        }
        catch (Exception ex)
        {
            ToastService.Instance.Show($"选择铃声失败: {ex.Message}", "error");
        }
    }

    // 卡片按压反馈（Seelen notifications: scale 0.9 @100ms 的轻量版）
    private static void OnNotificationPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border card) card.RenderTransform = TransformOperations.Parse("scale(0.97)");
    }

    private static void OnNotificationReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is Border card) card.RenderTransform = TransformOperations.Parse("scale(1)");
    }
}
