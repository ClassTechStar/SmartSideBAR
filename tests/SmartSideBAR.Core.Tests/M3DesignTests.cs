// MD3 设计令牌契约（2026-11，对齐 mochi-compose 规格）。
// 两类守卫：
//   1) JSON ↔ C# 默认值双向一致 —— 改了 design-tokens.json 忘改 DesignTokens.cs 会红；
//   2) 刻度自身合法 —— 形状递增、动效十档齐全、排版十五档齐全、触控达 MD3 下限。
using SmartSideBAR.Core.Design;

namespace SmartSideBAR.Core.Tests;

public class M3DesignTests
{
    private static M3Tokens Json => DesignTokens.Current.M3;
    private static readonly M3Tokens Defaults = new();

    // ---- 1. JSON ↔ C# 默认值一致 ----

    [Fact]
    public void 形状刻度_JSON与CSharp一致()
    {
        Assert.Equal(Defaults.Shape.ExtraSmall, Json.Shape.ExtraSmall);
        Assert.Equal(Defaults.Shape.Small, Json.Shape.Small);
        Assert.Equal(Defaults.Shape.Medium, Json.Shape.Medium);
        Assert.Equal(Defaults.Shape.Large, Json.Shape.Large);
        Assert.Equal(Defaults.Shape.LargeIncreased, Json.Shape.LargeIncreased);
        Assert.Equal(Defaults.Shape.ExtraLarge, Json.Shape.ExtraLarge);
        Assert.Equal(Defaults.Shape.ExtraLargeIncreased, Json.Shape.ExtraLargeIncreased);
        Assert.Equal(Defaults.Shape.ExtraExtraLarge, Json.Shape.ExtraExtraLarge);
        Assert.Equal(Defaults.Shape.Full, Json.Shape.Full);
    }

    [Fact]
    public void 间距与触控_JSON与CSharp一致()
    {
        Assert.Equal(Defaults.Spacing.Xs, Json.Spacing.Xs);
        Assert.Equal(Defaults.Spacing.S, Json.Spacing.S);
        Assert.Equal(Defaults.Spacing.M, Json.Spacing.M);
        Assert.Equal(Defaults.Spacing.L, Json.Spacing.L);
        Assert.Equal(Defaults.Spacing.Xl, Json.Spacing.Xl);
        Assert.Equal(Defaults.Spacing.Xxl, Json.Spacing.Xxl);
        Assert.Equal(Defaults.Touch.MinTargetDip, Json.Touch.MinTargetDip);
        Assert.Equal(Defaults.Touch.MinTouchDip, Json.Touch.MinTouchDip);
    }

    [Fact]
    public void 海拔六级_JSON与CSharp一致()
    {
        Assert.Equal(Defaults.Elevation.Level0, Json.Elevation.Level0);
        Assert.Equal(Defaults.Elevation.Level1, Json.Elevation.Level1);
        Assert.Equal(Defaults.Elevation.Level2, Json.Elevation.Level2);
        Assert.Equal(Defaults.Elevation.Level3, Json.Elevation.Level3);
        Assert.Equal(Defaults.Elevation.Level4, Json.Elevation.Level4);
        Assert.Equal(Defaults.Elevation.Level5, Json.Elevation.Level5);
    }

    [Fact]
    public void 排版十五档_JSON与CSharp一致()
    {
        AssertM3Text(Defaults.Typography.DisplayLarge, Json.Typography.DisplayLarge);
        AssertM3Text(Defaults.Typography.DisplayMedium, Json.Typography.DisplayMedium);
        AssertM3Text(Defaults.Typography.DisplaySmall, Json.Typography.DisplaySmall);
        AssertM3Text(Defaults.Typography.HeadlineLarge, Json.Typography.HeadlineLarge);
        AssertM3Text(Defaults.Typography.HeadlineMedium, Json.Typography.HeadlineMedium);
        AssertM3Text(Defaults.Typography.HeadlineSmall, Json.Typography.HeadlineSmall);
        AssertM3Text(Defaults.Typography.TitleLarge, Json.Typography.TitleLarge);
        AssertM3Text(Defaults.Typography.TitleMedium, Json.Typography.TitleMedium);
        AssertM3Text(Defaults.Typography.TitleSmall, Json.Typography.TitleSmall);
        AssertM3Text(Defaults.Typography.BodyLarge, Json.Typography.BodyLarge);
        AssertM3Text(Defaults.Typography.BodyMedium, Json.Typography.BodyMedium);
        AssertM3Text(Defaults.Typography.BodySmall, Json.Typography.BodySmall);
        AssertM3Text(Defaults.Typography.LabelLarge, Json.Typography.LabelLarge);
        AssertM3Text(Defaults.Typography.LabelMedium, Json.Typography.LabelMedium);
        AssertM3Text(Defaults.Typography.LabelSmall, Json.Typography.LabelSmall);
    }

    private static void AssertM3Text(M3TextStyle expected, M3TextStyle actual)
    {
        Assert.Equal(expected.Size, actual.Size);
        Assert.Equal(expected.LineHeight, actual.LineHeight);
        Assert.Equal(expected.Weight, actual.Weight);
        Assert.Equal(expected.LetterSpacing, actual.LetterSpacing);
    }

    // ---- 2. 刻度合法性 ----

    [Fact]
    public void 形状刻度严格递增且胶囊为最大值()
    {
        var scale = new[]
        {
            Json.Shape.ExtraSmall, Json.Shape.Small, Json.Shape.Medium, Json.Shape.Large,
            Json.Shape.LargeIncreased, Json.Shape.ExtraLarge, Json.Shape.ExtraLargeIncreased,
            Json.Shape.ExtraExtraLarge,
        };
        for (var i = 1; i < scale.Length; i++)
            Assert.True(scale[i] > scale[i - 1], $"形状刻度第 {i} 档未递增: {scale[i - 1]} → {scale[i]}");

        Assert.True(Json.Shape.Full >= 100, "full 应为胶囊语义（远大于任何具体圆角）");
    }

    [Fact]
    public void MD3形状锚点值正确()
    {
        // MD3 规范关键档位，防止有人"顺手调小一点"
        Assert.Equal(4, Json.Shape.ExtraSmall);
        Assert.Equal(8, Json.Shape.Small);
        Assert.Equal(12, Json.Shape.Medium);
        Assert.Equal(16, Json.Shape.Large);
        Assert.Equal(28, Json.Shape.ExtraLarge);
    }

    [Fact]
    public void 动效十档齐全且时长非负()
    {
        var specs = new[]
        {
            Json.Motion.Page, Json.Motion.Present, Json.Motion.Pop, Json.Motion.Control,
            Json.Motion.Settle, Json.Motion.Bounce, Json.Motion.PressDown,
            Json.Motion.PressRelease, Json.Motion.Fade, Json.Motion.FadeOut,
        };
        Assert.Equal(10, specs.Length);
        foreach (var spec in specs)
        {
            Assert.True(spec.Ms > 0, "动效时长必须为正");
            Assert.True(spec.ResponseSec > 0, "response 必须为正");
            Assert.InRange(spec.Damping, 0.1, 1.0);
            Assert.False(string.IsNullOrWhiteSpace(spec.Easing));
        }

        // 淡出比淡入短（mochi 规则：旧内容先让开）
        Assert.True(Json.Motion.FadeOut.Ms < Json.Motion.Fade.Ms);
    }

    [Fact]
    public void 动效档位时长符合mochi规格()
    {
        Assert.Equal(420, Json.Motion.Page.Ms);
        Assert.Equal(420, Json.Motion.Present.Ms);
        Assert.Equal(360, Json.Motion.Pop.Ms);
        Assert.Equal(340, Json.Motion.Control.Ms);
        Assert.Equal(280, Json.Motion.Settle.Ms);
        Assert.Equal(340, Json.Motion.Bounce.Ms);
        Assert.Equal(220, Json.Motion.PressDown.Ms);
        Assert.Equal(380, Json.Motion.PressRelease.Ms);
        Assert.Equal(150, Json.Motion.Fade.Ms);
        Assert.Equal(120, Json.Motion.FadeOut.Ms);
    }

    [Fact]
    public void 触控目标达到MD3与mochi下限()
    {
        Assert.True(Json.Touch.MinTargetDip >= 48, "MD3 默认触控目标应 ≥48");
        Assert.True(Json.Touch.MinTouchDip >= 44, "mochi 硬下限 44");
        Assert.True(Json.Touch.MinTouchDip <= Json.Touch.MinTargetDip);
    }

    [Fact]
    public void 排版十五档齐全且行高大于字号()
    {
        var styles = new[]
        {
            Json.Typography.DisplayLarge, Json.Typography.DisplayMedium, Json.Typography.DisplaySmall,
            Json.Typography.HeadlineLarge, Json.Typography.HeadlineMedium, Json.Typography.HeadlineSmall,
            Json.Typography.TitleLarge, Json.Typography.TitleMedium, Json.Typography.TitleSmall,
            Json.Typography.BodyLarge, Json.Typography.BodyMedium, Json.Typography.BodySmall,
            Json.Typography.LabelLarge, Json.Typography.LabelMedium, Json.Typography.LabelSmall,
        };
        Assert.Equal(15, styles.Length);
        foreach (var s in styles)
        {
            Assert.True(s.Size > 0);
            Assert.True(s.LineHeight > s.Size, "行高必须大于字号，否则中文行距塌陷");
            Assert.InRange(s.Weight, 300, 700);
        }
    }

    [Fact]
    public void MD3排版锚点值正确()
    {
        Assert.Equal(57, Json.Typography.DisplayLarge.Size);
        Assert.Equal(16, Json.Typography.BodyLarge.Size);
        Assert.Equal(14, Json.Typography.BodyMedium.Size);
        Assert.Equal(12, Json.Typography.BodySmall.Size);
        Assert.Equal(22, Json.Typography.TitleLarge.Size);
        Assert.Equal(500, Json.Typography.LabelLarge.Weight);
    }

    // ---- 3. 色彩角色 ----

    [Fact]
    public void 明暗两套色彩角色齐全且可解析()
    {
        foreach (var dark in new[] { false, true })
        {
            var set = dark ? Json.Color.Dark : Json.Color.Light;
            var roles = new[]
            {
                set.Primary, set.OnPrimary, set.PrimaryContainer, set.OnPrimaryContainer,
                set.Secondary, set.OnSecondary, set.SecondaryContainer,
                set.Error, set.OnError, set.ErrorContainer,
                set.Surface, set.OnSurface, set.SurfaceVariant, set.OnSurfaceVariant,
                set.Outline, set.OutlineVariant,
                set.SurfaceContainerLowest, set.SurfaceContainerLow, set.SurfaceContainer,
                set.SurfaceContainerHigh, set.SurfaceContainerHighest,
            };
            foreach (var hex in roles)
            {
                Assert.Matches("^#[0-9A-Fa-f]{6}$", hex);
            }
        }
    }

    [Fact]
    public void 亮色主色锚定品牌蓝()
    {
        Assert.Equal("#2B6EE0", Json.Color.Light.Primary);
        // 深色模式主色必须是同色系亮版，不是同一个值
        Assert.NotEqual(Json.Color.Light.Primary, Json.Color.Dark.Primary);
    }
}
