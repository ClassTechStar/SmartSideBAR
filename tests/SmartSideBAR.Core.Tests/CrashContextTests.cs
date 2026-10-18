// 崩溃上下文与安全模式（P2-3）。
using SmartSideBAR.Core.Diagnostics;

namespace SmartSideBAR.Core.Tests;

public class CrashContextTests
{
    [Fact]
    public void Safe_mode_reads_1_and_true_only()
    {
        Assert.True(SafeMode.IsEnabled("1"));
        Assert.True(SafeMode.IsEnabled(" true "));
        Assert.True(SafeMode.IsEnabled("TRUE"));
        Assert.False(SafeMode.IsEnabled("0"));
        Assert.False(SafeMode.IsEnabled(""));
        Assert.False(SafeMode.IsEnabled(null));
    }

    [Fact]
    public void Dpi_is_normalized_for_plausible_values_only()
    {
        Assert.Equal(1.5, SafeMode.NormalizeDpi(1.5));
        Assert.Equal(1.0, SafeMode.NormalizeDpi(0));   // 非法 → 100%
        Assert.Equal(1.0, SafeMode.NormalizeDpi(99));  // 离谱值 → 100%
    }

    [Fact]
    public void Os_build_string_has_three_components()
    {
        var text = SafeMode.OsBuildString(new Version(10, 0, 26100));
        Assert.Equal("10.0.26100", text);
    }

    [Fact]
    public void Unknown_context_is_never_null()
    {
        var ctx = CrashContext.Unknown;
        Assert.Equal("unknown", ctx.Version);
        Assert.Equal("none", ctx.ActivePanel);
    }

    [Fact]
    public void Context_is_immutable_and_replaceable()
    {
        var ctx = new CrashContext("1.3.0", 1.5, "Capture", "10.0.26100");
        var next = ctx with { ActivePanel = "Usb" };

        Assert.Equal("Capture", ctx.ActivePanel); // 原对象不变
        Assert.Equal("Usb", next.ActivePanel);
        Assert.Equal(1.5, next.DpiScale);
    }
}
