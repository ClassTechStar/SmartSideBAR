// 长截图目标窗口选择器 (longshot:selectWindow 落点)。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SmartSideBAR.Windows.Capture;

namespace SmartSideBAR.Avalonia.Ui;

public sealed class WindowPickerWindow(IReadOnlyList<LongshotTarget> targets) : Window
{
    public LongshotTarget? Selected { get; private set; }

    public WindowPickerWindow() : this([]) { }

    protected override void OnOpened(EventArgs e)
    {
        Title = "选择要长截图的窗口";
        Width = 460;
        Height = 520;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var list = new ListBox { Margin = new Thickness(12) };
        foreach (var t in targets) list.Items.Add(new TextBlock { Text = t.Title, Margin = new Thickness(4, 2) });

        var start = new Button
        {
            Content = "开始长截图 (3 秒倒计时)",
            Padding = new Thickness(16, 8),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        start.Click += (_, _) =>
        {
            if (list.SelectedIndex >= 0)
            {
                Selected = targets[list.SelectedIndex];
                Close();
            }
        };
        Content = new DockPanel
        {
            Margin = new Thickness(8),
            Children = { start, list },
        };
        DockPanel.SetDock(start, Dock.Bottom);
        base.OnOpened(e);
    }
}
