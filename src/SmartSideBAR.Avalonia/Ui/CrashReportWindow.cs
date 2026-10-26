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
    public CrashReportWindow(string summary, string logPath)
    {
        Title = "SmartSideBAR 上次运行异常";
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        SystemDecorations = SystemDecorations.BorderOnly;
        ShowInTaskbar = false;
        Topmost = true;

        var restartSafe = new Button
        {
            Content = "以安全模式重启",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 8),
        };
        restartSafe.Click += (_, _) => RestartSafeMode();

        var openLog = new Button
        {
            Content = "打开日志目录",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 8),
        };
        openLog.Click += (_, _) =>
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(dir)) Process.Start("explorer.exe", dir);
            }
            catch { /* 打开失败不影响 */ }
        };

        var close = new Button
        {
            Content = "知道了",
            HorizontalAlignment = HorizontalAlignment.Right,
            Padding = new Thickness(14, 8),
        };
        close.Click += (_, _) => Close();

        Content = new Border
        {
            Padding = new Thickness(20),
            Background = Brushes.White,
            Child = new StackPanel
            {
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "上次运行时发生了异常",
                        FontSize = 16,
                        FontWeight = FontWeight.SemiBold,
                    },
                    new TextBlock
                    {
                        Text = summary,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13,
                        Foreground = Brushes.DimGray,
                    },
                    new TextBlock
                    {
                        Text = "若反复出现, 可用安全模式启动（跳过应用栏与悬浮球）。",
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 12,
                        Foreground = Brushes.Gray,
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
            var psi = new ProcessStartInfo(exe) { UseShellExecute = true };
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
