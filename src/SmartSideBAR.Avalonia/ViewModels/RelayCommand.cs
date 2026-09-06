// 轻量命令 (无第三方依赖; Wave B 如引入 CommunityToolkit 可替换)
using System.Collections;
using System.Windows.Input;

namespace SmartSideBAR.Avalonia.ViewModels;

public sealed class RelayCommand(Action<object?> execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute(parameter);
}
