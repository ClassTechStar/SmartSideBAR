// 侧栏快捷链接行视图模型。
// B1  原 XAML 用 `{Binding $parent[Window].((vm:SidebarViewModel)DataContext).OpenLinkCommand}`
// 在编译绑定的 DataTemplate 闭包内无法解析 xmlns 前缀 (运行时抛 ArgumentException → 进程崩溃),
// 改为每行自带命令, 模板内不再做父级查找与类型转换。
using System.Windows.Input;
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.ViewModels;

public sealed class LinkRow
{
    public LinkRow(LinkItem source, Action<LinkRow> open)
    {
        Source = source;
        OpenCommand = new RelayCommand(_ => open(this));
    }

    public LinkItem Source { get; }

    public string Name => Source.Name;

    public string Url => Source.Url;

    public ICommand OpenCommand { get; }
}
