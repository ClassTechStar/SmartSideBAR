// 崩溃提示窗 (P0-4)。
// 借鉴 Class-Widgets-2 的 CrashHandler → CrashReport → 报告窗口: 崩溃不再静默退出,
// 下次启动时明确告知用户, 并给出"安全模式重启"的自救入口。
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace SmartSideBAR.Avalonia.Ui;

internal sealed class CrashReportWindow : Window
{
    /// <summary>palette 为 null 时按应用当前主题变体取 M3 语义色（深/浅自动）。</summary>
    public CrashReportWindow(string summary, string logPath, Palette? palette = null)
    {
        var p = palette ?? AppTheme.CurrentM3();
        // Fluent 控件模板按变体取色：只换 Palette 不设变体，按钮/下拉框仍是浅色白块
        RequestedThemeVariant = p.Dark
            ? global::Avalonia.Styling.ThemeVariant.Dark
            : global::Avalonia.Styling.ThemeVariant.Light;
        Title = "SmartSideBAR 上次运行异常";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SystemDecorations = SystemDecorations.BorderOnly;
        ShowInTaskbar = false;
        Topmost = true;

        var restartSafe = M3.Button("以安全模式重启", p, () => RestartSafeMode(), M3ButtonKind.Filled);
        restartSafe.HorizontalAlignment = HorizontalAlignment.Left;
        restartSafe.Click += (_, _) => RestartSafeMode();

        var openLog = M3.Button("打开日志目录", p, () => { }, M3ButtonKind.Tonal);
        openLog.HorizontalAlignment = HorizontalAlignment.Left;
        openLog.Click += (_, _) =>
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(dir)) Process.Start("explorer.exe", dir);
            }
            catch { /* 打开失败不影响 */ }
        };

        // 近景按钮：内容更新为实际动作（M3.Button 的 click 委托已设，这里追加原逻辑）

        var close = M3.Button("知道了", p, () => Close(), M3ButtonKind.Text);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.Click += (_, _) => Close();

        Content = new Border
        {
            Padding = new Thickness(20),
            Background = new SolidColorBrush(p.PanelBg),
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    M3.Type(new TextBlock
                    {
                        Text = "上次运行时发生了异常",
                        Foreground = new SolidColorBrush(p.Text),
                    }, M3TypeScale.TitleLarge),
                    new TextBlock
                    {
                        Text = summary,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13,
                        Foreground = new SolidColorBrush(p.Body),
                    },
                    new TextBlock
                    {
                        Text = "若反复出现, 可用安全模式启动（跳过应用栏与悬浮球）。",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 12,
                        Foreground = new SolidColorBrush(p.Muted),
                    },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 10,
                        Children = { restartSafe, openLog, close },
                    },
                },
            },
        };
    }

    private static void RestartSafeMode()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;
            // UseShellExecute=true 时 Environment 字典历来不可靠 (ShellExecuteEx 忽略),
            // 安全模式标记会被静默丢弃 → 改用直接进程启动 (exe 为绝对路径)
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
            psi.Environment["SSB_SAFE_MODE"] = "1";
            Process.Start(psi);
        }
        catch { /* 重启失败不阻塞 */ }

        Dispatcher.UIThread.Post(() =>
            (Application.Current?.ApplicationLifetime as
                global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            ?.Shutdown());
    }

    /// <summary>启动时若存在待确认崩溃, 显示提示窗。</summary>
    public static void ShowIfPending()
    {
        var summary = CrashLog.ConsumePending();
        if (summary is null) return;
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                new CrashReportWindow(summary, CrashLog.Path).Show();
            }
            catch { /* 提示失败不影响主流程 */ }
        });
    }
}
