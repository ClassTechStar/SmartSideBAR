// 主题注册表 (P1-3)。
// 借鉴 ClassIsland 的 XamlThemes 目录 + Class-Widgets-2 的 ThemeRecoveryController:
// 主题按 id 注册工厂, 生成失败时回退经典白面, 不让一次坏主题把界面拖成白屏。
// 现有两套主题(经典纯白 / 液态玻璃)的实现保持不变, 只是获得一个可回退的入口。
using SmartSideBAR.Core.Configuration;

namespace SmartSideBAR.Avalonia.Ui;

public static class ThemeRegistry
{
    public const string Classic = "classic";
    public const string LiquidGlass = "liquid-glass";

    private static readonly Dictionary<string, Func<AppearanceConfig?, Palette>> Factories =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<string> Ids => Factories.Keys;

    /// <summary>注册主题工厂(同 id 覆盖)。</summary>
    public static void Register(string id, Func<AppearanceConfig?, Palette> factory)
    {
        if (string.IsNullOrWhiteSpace(id)) return;
        Factories[id] = factory;
    }

    /// <summary>
    /// 按主题 id 解析调色板。未注册或工厂抛异常时回退到当前 <see cref="AppTheme.From"/> 的默认实现。
    /// </summary>
    public static Palette Resolve(string? themeId, AppearanceConfig? appearance)
    {
        if (!string.IsNullOrWhiteSpace(themeId)
            && Factories.TryGetValue(themeId, out var factory))
        {
            try
            {
                return factory(appearance);
            }
            catch
            {
                // 回退: 记录后走默认实现
            }
        }
        return AppTheme.From(appearance);
    }

    /// <summary>把当前两套主题登记进去(幂等)。</summary>
    public static void RegisterBuiltIns()
    {
        Register(Classic, a => AppTheme.From(WithGlass(a, false)));
        Register(LiquidGlass, a => AppTheme.From(WithGlass(a, true)));
    }

    private static AppearanceConfig? WithGlass(AppearanceConfig? a, bool glass)
    {
        if (a is null) return null;
        return a with { LiquidGlass = glass };
    }
}
