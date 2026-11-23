// 输入法角标 (2026-11 审计): 非中文布局不再输出裸十六进制片段。
using SmartSideBAR.Core.Input;

namespace SmartSideBAR.Core.Tests;

public sealed class ImeBadgeTests
{
    [Theory]
    [InlineData(0x0804, "zh-CN")] // 简体中文 (sublang=2)
    [InlineData(0x0404, "zh-TW")] // 繁体中文 (sublang=1)
    [InlineData(0x0C04, "zh-HK")] // 港式 (sublang=3)
    [InlineData(0x1004, "zh-SG")] // 新加坡 (sublang=4)
    public void 中文按子语言显示(ushort langId, string expected)
    {
        Assert.Equal(expected, ImeBadge.Format(langId));
    }

    [Theory]
    [InlineData(0x0411, "JPN")] // 日文
    [InlineData(0x0412, "KOR")] // 韩文
    [InlineData(0x0409, "ENG")] // 英文(美国)
    [InlineData(0x0407, "DEU")] // 德文
    public void 其他语言显示三字母ISO码(ushort langId, string expected)
    {
        Assert.Equal(expected, ImeBadge.Format(langId));
    }

    [Fact]
    public void 未知布局回退十六进制而不是裸数字()
    {
        var text = ImeBadge.Format(0xFFFE);
        Assert.StartsWith("0x", text);
    }
}
