// 全局字体 (MiSans Normal) 的资源与继承验证。
// 光编译通过不能证明字体真加载了 —— Avalonia 字体 URI 写错只会静默回退系统字体。
using System.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;

namespace SmartSideBAR.Avalonia.Tests;

public class FontTests
{
    private const string FontPath = "avares://SmartSideBAR.Avalonia/Assets/Fonts/MiSans-Normal.ttf";

    [Fact]
    public void 字体资源随程序集嵌入且是有效TTF()
    {
        Assert.True(AssetLoader.Exists(new Uri(FontPath)), "MiSans 字体未嵌入程序集资源");

        using var stream = AssetLoader.Open(new Uri(FontPath));
        var header = new byte[4];
        Assert.Equal(4, stream.Read(header, 0, 4));
        // TTF 文件头: 0x00010000 或 'true' / 'OTTO'
        Assert.True(
            header[0] == 0x00 && header[1] == 0x01 && header[2] == 0x00 && header[3] == 0x00,
            "字体资源不是有效的 TrueType 文件");
    }

    [Fact]
    public void 字体资源族名与全局样式一致()
    {
        // 与 App.axaml 的 FontFamily URI 后缀对齐，防止族名写错导致静默回退
        using var stream = AssetLoader.Open(new Uri(FontPath));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();

        var text = System.Text.Encoding.BigEndianUnicode.GetString(data);
        Assert.Contains("MiSans Normal", text);
    }

    [AvaloniaFact]
    public void 新建窗口默认继承MiSans字体()
    {
        var window = new Window();
        window.Show();

        var family = window.FontFamily;
        Assert.NotNull(family);
        Assert.Contains("MiSans Normal", family!.Name, StringComparison.OrdinalIgnoreCase);

        // 子元素也应继承（FontFamily 是可继承属性）
        var text = new TextBlock { Text = "字体测试" };
        window.Content = text;
        Assert.Contains("MiSans Normal", text.FontFamily.Name, StringComparison.OrdinalIgnoreCase);

        window.Close();
    }

    [Fact]
    public void 字体资源体积合理()
    {
        using var stream = AssetLoader.Open(new Uri(FontPath));
        Assert.True(stream.Length > 1_000_000, "字体文件异常小，可能复制的不是完整 TTF");
    }
}
