using Avalonia.Controls;
using SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Settings.Pages;

public sealed partial class CapturePage : UserControl
{
    public CapturePage(CaptureSettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public CaptureSettingsViewModel Vm => (CaptureSettingsViewModel)DataContext!;
}
