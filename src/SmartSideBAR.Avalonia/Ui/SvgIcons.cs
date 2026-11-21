// SVG 图标加载集中入口。
// B4  原实现依赖「Tag 标记 + 遍历可视树后置 Source」, 对 Button.Content 内的 Image
// 这样做无效: ContentPresenter 尚未由模板生成时 Image 不在可视树里, 扇形菜单图标会全空。
// 这里改为直接产出 IImage, 调用方在构造控件时即赋值, 与布局时序解耦。
using System.Collections.Concurrent;
using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace SmartSideBAR.Avalonia.Ui;

internal static class SvgIcons
{
    private static readonly Uri BaseUri = new("avares://SmartSideBAR.Avalonia/");

    // 借鉴 Flow.Launcher 的 ImageCache：面板/磁贴每次刷新都重建，SVG 解析（Skia 路径）
    // 重复做纯属浪费。缓存键含 white，因为同一图标有明暗两套资源。
    // 缓存 IImage 而非 SvgSource —— SvgImage 可被多个 Image 控件共享（只读资源）。
    private static readonly ConcurrentDictionary<(string Name, bool White), IImage?> Cache = new();

    /// <summary>加载 Assets/icons(-white)/{name}.svg; 缺失或解析失败返回 null (不阻断 UI)。</summary>
    public static IImage? Load(string name, bool white = false)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return Cache.GetOrAdd((name, white), static key => LoadCore(key.Name, key.White));
    }

    private static IImage? LoadCore(string name, bool white)
    {
        try
        {
            var dir = white ? "icons-white" : "icons";
            var source = SvgSource.Load($"Assets/{dir}/{name}.svg", BaseUri, null);
            return new SvgImage { Source = source };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>缓存条目数（测试与诊断用）。</summary>
    internal static int CacheCount => Cache.Count;

    /// <summary>清空缓存（资源热重载场景用）。</summary>
    internal static void ClearCache() => Cache.Clear();
}
