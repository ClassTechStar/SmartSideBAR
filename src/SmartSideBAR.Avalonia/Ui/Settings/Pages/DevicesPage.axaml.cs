using Avalonia.Controls;
using SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Settings.Pages;

public sealed partial class DevicesPage : UserControl
{
    public DevicesPage(DevicesSettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public DevicesSettingsViewModel Vm => (DevicesSettingsViewModel)DataContext!;
}
