using Avalonia.Controls;
using SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Settings.Pages;

public sealed partial class HotkeysPage : UserControl
{
    public HotkeysPage(HotkeysSettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public HotkeysSettingsViewModel Vm => (HotkeysSettingsViewModel)DataContext!;
}
