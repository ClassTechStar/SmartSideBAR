// 定时提醒窗 (补充 B7: rail 铃铛此前路由到不存在的 "reminder" 动作, 点击无任何反应)。
// 只做「快速添加一次性提醒」—— 周期/整点提醒由配置驱动, 本窗不承担编辑职责。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Scheduling;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class ReminderWindow : Window
{
    private readonly SchedulerService _scheduler;
    private readonly NumericUpDown _minutes = new() { Value = 10, Minimum = 1, Maximum = 720, Increment = 5 };
    private readonly TextBox _note = new() { Watermark = "提醒内容 (可留空)" };

    public ReminderWindow(SchedulerService scheduler)
    {
        _scheduler = scheduler;
        Title = "添加定时提醒";
        Width = 380;
        Height = 240;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = true;
        Content = BuildUi();
    }

    private Control BuildUi()
    {
        var add = new Button
        {
            Content = "添加",
            Padding = new Thickness(20, 8),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        add.Click += (_, _) => Add();

        var cancel = new Button { Content = "取消", Padding = new Thickness(20, 8) };
        cancel.Click += (_, _) => Close();

        static StackPanel Row(string label, Control control)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 6) };
            sp.Children.Add(new TextBlock { Text = label, Width = 84, VerticalAlignment = VerticalAlignment.Center });
            sp.Children.Add(control);
            return sp;
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0),
        };
        buttons.Children.Add(cancel);
        buttons.Children.Add(add);

        var panel = new StackPanel
        {
            Margin = new Thickness(20),
            Children =
            {
                new TextBlock { Text = "N 分钟后提醒我", FontSize = 15, FontWeight = FontWeight.SemiBold },
                Row("倒计时 (分钟)", _minutes),
                Row("内容", _note),
                buttons,
            },
        };
        return panel;
    }

    private void Add()
    {
        var minutes = (int)Math.Clamp(_minutes.Value ?? 10, 1, 720);
        var note = string.IsNullOrWhiteSpace(_note.Text) ? $"{minutes} 分钟到了" : _note.Text.Trim();
        var at = DateTimeOffset.Now.AddMinutes(minutes).ToUnixTimeMilliseconds();
        _scheduler.AddReminder(ReminderKind.Once, at, note);
        ToastService.Instance.Show($"已添加提醒: {minutes} 分钟后 · {note}", "success");
        Close();
    }
}
