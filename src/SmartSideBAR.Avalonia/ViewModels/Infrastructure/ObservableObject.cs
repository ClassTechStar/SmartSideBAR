// MVVM 基座 (M0): 极简 INotifyPropertyChanged 基类。
// 不引入 CommunityToolkit 等框架 —— 现有 RelayCommand/手写 INPC 已够用,
// 这里只补齐 SetProperty/Raise 的重复样板, 供新 State/VM 继承。
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SmartSideBAR.Avalonia.ViewModels.Infrastructure;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>值变化时写入并通知; 相等则不通知并返回 false。</summary>
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(propertyName);
        return true;
    }

    protected void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
