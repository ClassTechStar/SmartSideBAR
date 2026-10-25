// 跟随 Windows 系统强调色（Seelen 同款能力）: appearance.accent = "auto" 时读取
// WinRT UISettings.Accent。TFM 为 net10.0-windows10.0.19041.0, SDK 投影直接可用。
// 读取失败（老系统/权限）回退品牌蓝, 不能阻断启动。
using Avalonia.Media;

namespace SmartSideBAR.Avalonia.Ui;

public static class SystemAccent
{
    public static Color Get()
    {
        try
        {
            var ui = new global::Windows.UI.ViewManagement.UISettings();
            var accent = ui.GetColorValue(global::Windows.UI.ViewManagement.UIColorType.Accent);
            return Color.FromArgb(accent.A, accent.R, accent.G, accent.B);
        }
        catch
        {
            return Color.Parse("#2B6EE0");
        }
    }
}
