// SettingsPanel —— v1.2 同名面板的 Avalonia 实现。
// P2-1：由 697 行的 SidebarPanels.cs 拆出，一面板一文件（单文件 ≤600 行的拆分约定），
// 便于单独单测、减少合并冲突。公共构件见 PanelKit.cs，基类见 SidebarPanel.cs。
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SmartSideBAR.Avalonia.ViewModels;
using SmartSideBAR.Avalonia.Views;
using SmartSideBAR.Core.Configuration;
using SmartSideBAR.Core.Devices;
using SmartSideBAR.Core.Scheduling;
using SmartSideBAR.Windows.Devices;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal sealed class SettingsPanel(SidebarServices s, Palette p) : SidebarPanel(s, p)
{
    protected override Control Build()
    {
        var cfg = Services.Config.Current;
        var root = Stack(14);

        root.Children.Add(PanelKit.Section("外观", Palette));
        var theme = new ComboBox
        {
            ItemsSource = new[] { "浅色（默认）", "深色" },
            SelectedIndex = string.Equals(cfg.Appearance.Theme, "dark", StringComparison.OrdinalIgnoreCase) ? 1 : 0,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        theme.SelectionChanged += (_, _) =>
        {
            var value = theme.SelectedIndex == 1 ? "dark" : "light";
            Services.Config.Set("appearance.theme", value);
            (TopLevel.GetTopLevel(this) as SidebarWindow)?.ApplyTheme();
        };
        root.Children.Add(theme);

        root.Children.Add(PanelKit.Section("启动", Palette));
        var autoLaunch = new CheckBox
        {
            Content = "开机自动启动",
            IsChecked = Services.Shell.GetAutoLaunch(),
            FontSize = 13,
        };
        autoLaunch.IsCheckedChanged += (_, _) =>
        {
            try { Services.Shell.SetAutoLaunch(autoLaunch.IsChecked == true); }
            catch (Exception ex) { ToastService.Instance.Show($"设置自启动失败: {ex.Message}", "error"); }
        };
        root.Children.Add(autoLaunch);

        root.Children.Add(PanelKit.Divider(Palette));
        root.Children.Add(PanelKit.Flat("打开完整设置…", Palette, () => Services.Dispatch("settings"), primary: true));
        root.Children.Add(PanelKit.Hint("热键、截图格式、录屏帧率、侧栏停靠边等高级选项在完整设置中。", Palette));
        return root;
    }
}
