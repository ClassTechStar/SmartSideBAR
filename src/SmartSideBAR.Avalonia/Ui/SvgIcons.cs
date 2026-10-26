// SVG 图标加载集中入口。
// B4  原实现依赖「Tag 标记 + 遍历可视树后置 Source」, 对 Button.Content 内的 Image
// 这样做无效: ContentPresenter 尚未由模板生成时 Image 不在可视树里, 扇形菜单图标会全空。
// 这里改为直接产出 IImage, 调用方在构造控件时即赋值, 与布局时序解耦。
using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace SmartSideBAR.Avalonia.Ui;

internal static class SvgIcons
{
    private static readonly Uri BaseUri = new("avares://SmartSideBAR.Avalonia/");

    /// <summary>加载 Assets/icons(-white)/{name}.svg; 缺失或解析失败返回 null (不阻断 UI)。</summary>
    public static IImage? Load(string name, bool white = false)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
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
}
