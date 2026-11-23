using Avalonia.Controls;
using SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Settings.Pages;

public sealed partial class RecorderPage : UserControl
{
    public RecorderPage(RecorderSettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public RecorderSettingsViewModel Vm => (RecorderSettingsViewModel)DataContext!;
}
