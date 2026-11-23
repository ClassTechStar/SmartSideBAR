// 面板公共度量 (P0-2)。M3: 8 个面板已迁到 XAML + PanelStyles.axaml,
// 这里只保留 DPI 缩放折算, 供 PanelTheme 写入触屏最小点击区。
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Ui.Panels;

internal static class PanelKit
{
    /// <summary>
    /// 当前 UI 缩放（1.0 = 100%）。由窗口层在 DPI/缩放变化时写入，
    /// 用于把「物理最小点击区」折算成 DIP。未设置时按 1.0 处理。
    /// </summary>
    public static double UiScale { get; set; } = 1.0;

    /// <summary>触屏最小点击区折算成 DIP；令牌为 0 时返回 0（不启用约束）。</summary>
    public static double MinHitDip
    {
        get
        {
            var min = DesignTokens.Current.Touch.MinHitPx;
            if (min <= 0) return 0;
            return min / Math.Max(0.25, UiScale);
        }
    }
}
