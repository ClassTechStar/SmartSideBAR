// P1-7 长截图守卫: 黑块检测。BitBlt 对 GPU 加速窗口会得到整块黑色,
// 若不检测直接拼接, 长图会混入黑块/重影, 比失败更伤信任。
using System.Drawing;
using SmartSideBAR.Windows.Capture;

namespace SmartSideBAR.Windows.Tests;

public sealed class LongshotFrameGuardTests
{
    [Fact]
    public void 全黑帧判定为黑帧()
    {
        using var bmp = new Bitmap(320, 240);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
        }
        Assert.True(LongshotService.IsMostlyBlack(bmp));
    }

    [Fact]
    public void 正常画面不判黑()
    {
        using var bmp = new Bitmap(320, 240);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.White);
            g.FillRectangle(Brushes.SteelBlue, 20, 20, 200, 120);
        }
        Assert.False(LongshotService.IsMostlyBlack(bmp));
    }

    [Fact]
    public void 近黑但有内容的帧不误判()
    {
        using var bmp = new Bitmap(320, 240);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            // 约 2% 采样点亮起 (> 0.5% 阈值) —— 微弱内容不应按黑帧丢弃
            g.FillRectangle(Brushes.White, 0, 0, 320, 12);
        }
        Assert.False(LongshotService.IsMostlyBlack(bmp));
    }
}
