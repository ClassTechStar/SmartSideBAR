// 插件契约 v1 (E2): 动作执行器。
// 只认白名单命令; 执行前再校验一次 (防清单在运行期被替换)。真实系统动作经 IActionSink 注入,
// 便于单测路由与拒绝逻辑, 也让 Avalonia 之外的宿主可复用。
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Actions;

namespace SmartSideBAR.Avalonia.Actions;

/// <summary>动作落地能力 (打开浏览器/资源管理器/剪贴板/提示/内置动作)。</summary>
public interface IActionSink
{
    void OpenUrl(string url);

    void OpenPath(string path);

    void CopyText(string text);

    void Toast(string message, string level);

    void Dispatch(string action);
}

public sealed class ActionExecutor(IActionSink sink)
{
    /// <summary>执行一条已加载的动作; 失败返回可读原因 (不抛异常)。</summary>
    public bool TryExecute(ActionDefinition action, out string? error)
    {
        error = null;
        if (!ActionValidation.ValidateAction(action, out error)) return false;
        var command = action.Command!;
        try
        {
            switch (command.Type.Trim().ToLowerInvariant())
            {
                case ActionCommandTypes.OpenUrl:
                    sink.OpenUrl(command.Url!);
                    break;
                case ActionCommandTypes.OpenPath:
                    if (!ActionValidation.IsAllowedPath(command.Path, out var normalized))
                    {
                        error = $"路径不在允许范围: {command.Path}";
                        return false;
                    }
                    sink.OpenPath(normalized!);
                    break;
                case ActionCommandTypes.Dispatch:
                    sink.Dispatch(command.Action!);
                    break;
                case ActionCommandTypes.Workspace:
                    sink.Dispatch($"workspace:{command.Workspace!.Trim()}");
                    break;
                case ActionCommandTypes.CopyText:
                    sink.CopyText(command.Text!);
                    break;
                case ActionCommandTypes.Toast:
                    sink.Toast(command.Text!, command.Level ?? "info");
                    break;
                default:
                    error = $"不支持的命令类型: {command.Type}";
                    return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

/// <summary>生产环境落地: 浏览器 / 资源管理器 / 主窗口剪贴板 / Toast / 现有动作路由。</summary>
public sealed class WindowActionSink(Action<string> dispatch) : IActionSink
{
    public void OpenUrl(string url) =>
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public void OpenPath(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = false });

    public void CopyText(string text)
    {
        var clipboard = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow?.Clipboard;
        if (clipboard is null) throw new InvalidOperationException("剪贴板不可用");
        _ = clipboard.SetTextAsync(text);
    }

    public void Toast(string message, string level) => ToastService.Instance.Show(message, level);

    public void Dispatch(string action) => dispatch(action);
}
