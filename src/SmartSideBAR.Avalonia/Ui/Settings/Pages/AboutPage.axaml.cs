using Avalonia.Controls;
using SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Settings.Pages;

public sealed partial class AboutPage : UserControl
{
    public AboutPage(AboutSettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public AboutSettingsViewModel Vm => (AboutSettingsViewModel)DataContext!;
}
