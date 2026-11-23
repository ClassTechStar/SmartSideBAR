using Avalonia.Controls;
using SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Settings.Pages;

public sealed partial class DiagnosticsPage : UserControl
{
    public DiagnosticsPage(DiagnosticsSettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public DiagnosticsSettingsViewModel Vm => (DiagnosticsSettingsViewModel)DataContext!;
}
