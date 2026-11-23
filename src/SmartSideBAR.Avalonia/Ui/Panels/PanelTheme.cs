// M3/M5: XAML 面板的主题桥接。
// 颜色不再由面板各自映射 Palette —— 统一走 ThemeResources.ApplyContent（与设置页同一个出口）;
// 这里只补面板私有的几何令牌（圆角/触控最小高度）。
// 面板资源字典是"隔离层": 面板在侧栏窗口内跟随窗口主题, 在测试宿主里也能独立渲染。
using Avalonia;
using Avalonia.Controls;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal static class PanelTheme
{
    public static void Apply(IResourceDictionary resources, Palette p)
    {
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(p);

        ThemeResources.ApplyContent(resources, p);

        var shape = DesignTokens.Current.Shape;
        resources["PanelCardRadius"] = new CornerRadius(shape.RadiusM);
        resources["PanelSmallRadius"] = new CornerRadius(shape.RadiusS);
        // 触屏最小点击区（与 PanelKit.MinHitDip 同源）
        resources["PanelMinTile"] = Math.Max(60, PanelKit.MinHitDip);
    }
}
