// 常规页代码后置: 只负责把 VM 绑到 DataContext。
using Avalonia.Controls;
using SmartSideBAR.Avalonia.Ui.Settings.ViewModels;

namespace SmartSideBAR.Avalonia.Ui.Settings.Pages;

public sealed partial class GeneralPage : UserControl
{
    public GeneralPage(GeneralSettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    public GeneralSettingsViewModel Vm => (GeneralSettingsViewModel)DataContext!;
}
