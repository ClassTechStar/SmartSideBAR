// MD3 组件规格守卫（Avalonia 层）。
// 不渲染像素，只断言"工厂产出的控件符合规范数值"——这类偏差肉眼难察、代码里极易漂移。
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Controls;
using Avalonia.Media;
using SmartSideBAR.Avalonia.Ui;
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Avalonia.Tests;

public class M3ComponentTests
{
    private static Palette TestPalette() => new()
    {
        Dark = false,
        Glass = false,
        PanelBg = Colors.White,
        Inset = Color.Parse("#F5F7FA"),
        Card = Colors.White,
        Text = Color.Parse("#1A1A1A"),
        Body = Color.Parse("#1B2230"),
        CardText = Color.Parse("#2A3240"),
        Muted = Color.Parse("#5A6478"),
        Divider = Color.Parse("#E8E8E8"),
        Hover = Color.Parse("#F5F7FA"),
        Accent = Color.Parse("#2B6EE0"),
        AccentSoft = Color.Parse("#E8F1FC"),
        RailFrom = Colors.White,
        RailTo = Colors.White,
        RailGlow = Colors.White,
        RimFrom = Colors.White,
        RimTo = Colors.White,
    };

    private static M3Tokens T => DesignTokens.Current.M3;

    [AvaloniaFact]
    public void 按钮高度与内边距符合MD3()
    {
        var p = TestPalette();
        foreach (var kind in new[] { M3ButtonKind.Filled, M3ButtonKind.Tonal, M3ButtonKind.Outlined, M3ButtonKind.Text })
        {
            var b = M3.Button("确定", p, () => { }, kind);
            Assert.Equal(T.Touch.MinTargetDip, b.MinHeight);
            Assert.Equal(new Thickness(24, 12), b.Padding);
            // MD3 按钮是胶囊
            Assert.Equal(new CornerRadius(T.Shape.Full), b.CornerRadius);
        }
    }

    [AvaloniaFact]
    public void 按钮变体背景互不相同()
    {
        var p = TestPalette();
        var filled = M3.Button("a", p, () => { }, M3ButtonKind.Filled);
        var tonal = M3.Button("b", p, () => { }, M3ButtonKind.Tonal);
        var text = M3.Button("c", p, () => { }, M3ButtonKind.Text);

        Assert.NotEqual(((ISolidColorBrush)filled.Background!).Color, ((ISolidColorBrush)tonal.Background!).Color);
        Assert.Equal(Colors.Transparent, ((ISolidColorBrush)text.Background!).Color);
    }

    [AvaloniaFact]
    public void 卡片圆角与海拔符合MD3()
    {
        var card = M3.Card(new TextBlock { Text = "内容" }, TestPalette());
        Assert.Equal(new CornerRadius(T.Shape.Medium), card.CornerRadius);
        Assert.Equal(new Thickness(16), card.Padding);
        // elevation 1 至少有一条阴影
        Assert.True(card.BoxShadow.Count > 0);
    }

    [AvaloniaFact]
    public void 输入框高度与圆角符合MD3()
    {
        var field = M3.TextField("请输入", TestPalette());
        Assert.Equal(T.Touch.MinTargetDip, field.MinHeight);
        Assert.Equal(new CornerRadius(T.Shape.Small), field.CornerRadius);
        Assert.Equal(new Thickness(16, 12), field.Padding);
    }

    [AvaloniaFact]
    public void Chip高度与圆角符合MD3()
    {
        var chip = M3.Chip("标签", TestPalette());
        Assert.Equal(32, chip.MinHeight);
        Assert.Equal(new CornerRadius(T.Shape.Small), chip.CornerRadius);

        var selected = M3.Chip("标签", TestPalette(), selected: true);
        Assert.NotEqual(((ISolidColorBrush)chip.Background!).Color, ((ISolidColorBrush)selected.Background!).Color);
    }

    [AvaloniaFact]
    public void 列表行单行与双行高度符合MD3()
    {
        var p = TestPalette();
        var single = M3.ListRow(null, "标题", null, null, p);
        var twoLine = M3.ListRow(null, "标题", "副标题", null, p);

        Assert.Equal(T.Touch.MinTargetDip, single.MinHeight);
        Assert.Equal(56, twoLine.MinHeight);
    }

    [AvaloniaFact]
    public void 触控兜底不低于44dip()
    {
        var small = M3.EnsureTouchTarget(new Button { MinHeight = 10, MinWidth = 10 });
        Assert.True(small.MinHeight >= T.Touch.MinTouchDip);
        Assert.True(small.MinWidth >= T.Touch.MinTouchDip);
    }

    [AvaloniaFact]
    public void 排版按档位设置字号行高字重()
    {
        var text = M3.Type(new TextBlock(), M3TypeScale.TitleMedium);
        Assert.Equal(T.Typography.TitleMedium.Size, text.FontSize);
        Assert.Equal(T.Typography.TitleMedium.LineHeight, text.LineHeight);
        Assert.Equal(FontWeight.Medium, text.FontWeight);
    }

    [AvaloniaFact]
    public void 缓动名可解析且未知名安全回落()
    {
        Assert.IsType<global::Avalonia.Animation.Easings.CubicEaseOut>(M3.Ease("CubicEaseOut"));
        Assert.IsType<global::Avalonia.Animation.Easings.BackEaseOut>(M3.Ease("BackEaseOut"));
        Assert.IsType<global::Avalonia.Animation.Easings.LinearEasing>(M3.Ease("不存在的缓动"));
        Assert.IsType<global::Avalonia.Animation.Easings.LinearEasing>(M3.Ease(null));
    }

    [AvaloniaFact]
    public void 动效时长与令牌一致()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(T.Motion.Page.Ms), M3.PageTime);
        Assert.Equal(TimeSpan.FromMilliseconds(T.Motion.Settle.Ms), M3.SettleTime);
        Assert.Equal(TimeSpan.FromMilliseconds(T.Motion.FadeOut.Ms), M3.FadeOutTime);
    }

    [AvaloniaFact]
    public void 海拔级别单调递增()
    {
        // 级别越高阴影层数不减少（MD3 elevation 语义：越高越"浮"）
        for (var level = 1; level < 5; level++)
        {
            Assert.True(M3.Elevation(level + 1).Count >= M3.Elevation(level).Count,
                $"elevation {level + 1} 的阴影层数不应少于 {level}");
        }
    }

    [AvaloniaFact]
    public void 色彩角色明暗两套可解析()
    {
        Assert.Equal(Color.Parse("#2B6EE0"), M3.Role(M3ColorRole.Primary));
        Assert.NotEqual(M3.Role(M3ColorRole.Primary), M3.Role(M3ColorRole.Primary, dark: true));
        Assert.Equal(255, M3.Role(M3ColorRole.SurfaceContainerHighest).A); // 语义色必须不透明
    }

    // ---- 一致性校验：M3 适配层自身不得出现硬编码 ----

    private static string M3Source => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "SmartSideBAR.Avalonia", "Ui", "M3.cs"));

    [AvaloniaFact]
    public void M3适配层无硬编码颜色()
    {
        // 允许 Colors.Transparent / Colors.White 这类语义常量，禁止十六进制字面量
        var matches = System.Text.RegularExpressions.Regex.Matches(M3Source, @"""#[0-9A-Fa-f]{3,8}""");
        Assert.True(matches.Count == 0,
            $"M3.cs 出现 {matches.Count} 处硬编码颜色，应改用令牌：{string.Join(", ", matches.Select(m => m.Value))}");
    }

    [AvaloniaFact]
    public void M3适配层无裸圆角数字()
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(M3Source, @"new CornerRadius\(\s*\d");
        Assert.True(matches.Count == 0, $"M3.cs 出现 {matches.Count} 处裸圆角数字，应改用 M3 形状刻度");
    }

    [AvaloniaFact]
    public void M3适配层无裸动画时长数字()
    {
        // TimeSpan.FromMilliseconds(数字) 只允许出现在 Ms() 桥接方法里
        var matches = System.Text.RegularExpressions.Regex.Matches(M3Source, @"TimeSpan\.FromMilliseconds\(\s*\d");
        Assert.True(matches.Count <= 1, $"M3.cs 出现 {matches.Count} 处裸时长，应统一经 Ms(M3MotionSpec) 读取令牌");
    }
}
