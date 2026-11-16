// §6.4 长截图: EnumWindows 定位 + BitBlt 滚动帧 + 线性拼接。
// 跨屏/DPI 用 DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS) 物理矩形 (A5 修复)。
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Capture;
using SmartSideBAR.Core.Messaging;
using SmartSideBAR.Windows.AppBar;

namespace SmartSideBAR.Windows.Capture;

public sealed record LongshotTarget(nint Hwnd, string Title);
public sealed record LongshotProgress(int HeightPx, int Frames);
public sealed record LongshotCountdown(int Seconds);
public sealed record LongshotCompleted(string? FilePath, string? Error);

public interface ILongshotService
{
    IReadOnlyList<LongshotTarget> EnumTargets();
    Task<string?> CaptureAsync(LongshotTarget target, string dir, string format, CancellationToken ct = default);
}

public sealed partial class LongshotService(IEventBus bus, ILogger<LongshotService>? log = null) : ILongshotService
{
    private const int MaxHeightPx = 24000;
    private const int MaxFrames = 120;
    private const int ScrollWaitMs = 550;
    private const uint VkNext = 0x22;
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;

    public IReadOnlyList<LongshotTarget> EnumTargets()
    {
        var list = new List<LongshotTarget>();
        using var scope = new AppBarApi.PhysicalDpiScope();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            if (GetWindowTextLength(hwnd) == 0) return true;
            if (GetAncestor(hwnd, GA_ROOTOWNER) != GetLastActivePopup(hwnd)) return true;
            // DWM cloaked (UWP 挂起窗口) 过滤
            if (IsCloaked(hwnd)) return true;
            var sb = new System.Text.StringBuilder(256);
            _ = GetWindowText(hwnd, sb, 256);
            var title = sb.ToString().Trim();
            if (title.Length == 0) return true;
            if (GetWindowRect(hwnd, out var r) && r.Width < 200) return true;
            list.Add(new LongshotTarget(hwnd, title));
            return true;
        }, 0);
        return list;
    }

    public async Task<string?> CaptureAsync(LongshotTarget target, string dir, string format, CancellationToken ct = default)
    {
        try
        {
            // 3 秒倒计时 (longshot:countdown 对应事件), 给用户切窗时间
            for (var s = 3; s > 0; s--)
            {
                bus.Publish(new LongshotCountdown(s));
                await Task.Delay(1000, ct).ConfigureAwait(false);
            }

            _ = SetForegroundWindow(target.Hwnd);
            await Task.Delay(250, ct).ConfigureAwait(false);

            var rect = GetExtendedFrameBounds(target.Hwnd);
            if (rect.W <= 0 || rect.H <= 0) throw new InvalidOperationException("目标窗口矩形不可用");

            Bitmap canvas = CaptureFrame(rect) ?? throw new InvalidOperationException("首帧捕获失败");
            // P1-7 黑块检测 (首帧): 整帧近黑说明 BitBlt 对该窗口失效 (典型: GPU 加速的 Chromium 类窗口)
            if (IsMostlyBlack(canvas))
            {
                canvas.Dispose();
                throw new InvalidOperationException("窗口内容无法捕获（GPU 加速渲染的窗口不支持 BitBlt 截取），请改用区域截图");
            }

            var frames = 1;
            var committed = canvas.Height;
            while (frames < MaxFrames && committed < MaxHeightPx)
            {
                ct.ThrowIfCancellationRequested();
                _ = PostMessage(target.Hwnd, WmKeyDown, VkNext, 0);
                _ = PostMessage(target.Hwnd, WmKeyUp, VkNext, 0);
                await Task.Delay(ScrollWaitMs, ct).ConfigureAwait(false);

                var frame = CaptureFrame(rect);
                if (frame is null) break;
                // P1-7 黑块检测: 中途拿到黑帧 (GPU 窗口滚动重绘失效) 就此收尾, 不把黑色拼进长图
                if (IsMostlyBlack(frame))
                {
                    frame.Dispose();
                    log?.LogWarning("[Longshot] 第 {N} 帧近乎全黑 (BitBlt 失效), 提前收尾保留已拼内容", frames + 1);
                    break;
                }

                // B6: 参与比对的必须是「已拼图的尾部若干行」, 不能传 RowHashes(canvas, n)
                // 那是画布顶部 n 行, 与「尾部重叠」语义相反。
                var overlap = FindOverlapFor(canvas, frame);
                if (overlap >= frame.Height)
                {
                    frame.Dispose();
                    break; // 已到底
                }

                // P1-7 拼接校验: 重叠为 0 说明滚动没推进或窗口内容变化过大, 直接拼会重影。
                // 重试一拍; 仍为 0 则显式失败, 不产出错误长图。
                if (overlap == 0)
                {
                    frame.Dispose();
                    await Task.Delay(ScrollWaitMs, ct).ConfigureAwait(false);
                    frame = CaptureFrame(rect);
                    if (frame is null) break;
                    if (IsMostlyBlack(frame))
                    {
                        frame.Dispose();
                        log?.LogWarning("[Longshot] 重试帧近乎全黑, 提前收尾保留已拼内容");
                        break;
                    }
                    overlap = FindOverlapFor(canvas, frame);
                    if (overlap == 0)
                    {
                        frame.Dispose();
                        canvas.Dispose();
                        throw new InvalidOperationException("滚动拼接校验失败（滚动未推进或窗口内容持续变化），已中止以免输出重影长图");
                    }
                    if (overlap >= frame.Height)
                    {
                        frame.Dispose();
                        break; // 已到底
                    }
                }

                canvas = Stitch(canvas, frame, overlap);
                committed = canvas.Height;
                frames++;
                bus.Publish(new LongshotProgress(committed, frames));
            }

            Directory.CreateDirectory(dir);
            var ext = format.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? "jpg" : "png";
            var path = Path.Combine(dir, $"longshot_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}");
            canvas.Save(path, ext == "jpg" ? ImageFormat.Jpeg : ImageFormat.Png);
            canvas.Dispose();
            bus.Publish(new LongshotCompleted(path, null));
            log?.LogInformation("[Longshot] 完成 {Path} ({Frames} 帧)", path, frames);
            return path;
        }
        catch (OperationCanceledException)
        {
            bus.Publish(new LongshotCompleted(null, "已取消"));
            return null;
        }
        catch (Exception ex)
        {
            log?.LogError(ex, "[Longshot] 失败");
            bus.Publish(new LongshotCompleted(null, ex.Message));
            return null;
        }
    }

    private static Bitmap? CaptureRect(AppBarRect r)
    {
        var bmp = new Bitmap(r.W, r.H, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        var hdcDest = g.GetHdc();
        var hdcSrc = GetDC(0);
        _ = BitBlt(hdcDest, 0, 0, r.W, r.H, hdcSrc, r.X, r.Y, SRCCOPY | CAPTUREBLT);
        g.ReleaseHdc(hdcDest);
        _ = ReleaseDC(0, hdcSrc);
        return bmp;
    }

    /// <summary>在物理 DPI 作用域内抓一帧 (BitBlt 坐标是物理像素)。</summary>
    private static Bitmap? CaptureFrame(AppBarRect r)
    {
        using var scope = new AppBarApi.PhysicalDpiScope();
        return CaptureRect(r);
    }

    /// <summary>新帧与已拼图尾部的最大行重叠 (B6 语义: 新帧顶部 == 尾部末行)。</summary>
    private static int FindOverlapFor(Bitmap canvas, Bitmap frame)
    {
        var overlapWindow = Math.Min(canvas.Height, frame.Height);
        var tailHashes = RowHashes(canvas, canvas.Height - overlapWindow, overlapWindow);
        var frameHashes = RowHashes(frame, 0, frame.Height);
        return LongshotStitcher.FindOverlap(tailHashes, frameHashes, overlapWindow);
    }

    /// <summary>
    /// P1-7 黑块检测: BitBlt 对 GPU 加速窗口 (Chromium 类) 会得到整块黑色。
    /// 32×32 网格采样, 非黑采样点不足 0.5% 判定为黑帧。
    /// </summary>
    internal static bool IsMostlyBlack(Bitmap bmp)
    {
        const int grid = 32;
        var nonBlack = 0;
        for (var gy = 0; gy < grid; gy++)
        {
            for (var gx = 0; gx < grid; gx++)
            {
                var x = (int)((long)bmp.Width * gx / grid);
                var y = (int)((long)bmp.Height * gy / grid);
                var c = bmp.GetPixel(Math.Min(x, bmp.Width - 1), Math.Min(y, bmp.Height - 1));
                if (c.R > 8 || c.G > 8 || c.B > 8) nonBlack++;
            }
        }
        return nonBlack < grid * grid / 200;
    }

    private static Bitmap Stitch(Bitmap tail, Bitmap frame, int overlap)
    {
        var newH = tail.Height + frame.Height - overlap;
        var stitched = new Bitmap(Math.Max(tail.Width, frame.Width), newH, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(stitched);
        g.DrawImageUnscaled(tail, 0, 0);
        g.DrawImageUnscaled(frame, 0, tail.Height - overlap);
        tail.Dispose();
        frame.Dispose();
        return stitched;
    }

    /// <summary>行哈希: 逐行采样求和 (快而稳; 完全一致才计重叠)。
    /// startRow 允许只取图像的一段 (例如「尾部若干行」), 供重叠判定使用。</summary>
    internal static long[] RowHashes(Bitmap bmp, int startRow, int rowCount)
    {
        var count = Math.Max(0, Math.Min(rowCount, bmp.Height - startRow));
        var hashes = new long[count];
        if (count == 0) return hashes;
        var rect = new Rectangle(0, startRow, bmp.Width, count);
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        unsafe
        {
            var stride = data.Stride;
            var basePtr = (byte*)data.Scan0;
            var step = Math.Max(1, bmp.Width / 64); // 每行最多采样 64 点
            for (var y = 0; y < count; y++)
            {
                long h = 0;
                var row = basePtr + y * stride;
                for (var x = 0; x < bmp.Width; x += step)
                {
                    var p = row + x * 4;
                    h = h * 31 + (p[0] + p[1] * 3 + p[2] * 7);
                }
                hashes[y] = h;
            }
        }
        bmp.UnlockBits(data);
        return hashes;
    }

    private static AppBarRect GetExtendedFrameBounds(nint hwnd)
    {
        var hr = DwmGetWindowAttributeRect(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out var physical, Marshal.SizeOf<NativeRect>());
        if (hr == 0) return new AppBarRect(physical.Left, physical.Top, physical.Width, physical.Height);
        _ = GetWindowRect(hwnd, out var fallback);
        return new AppBarRect(fallback.Left, fallback.Top, fallback.Width, fallback.Height);
    }

    private static bool IsCloaked(nint hwnd) =>
        DwmGetWindowAttributeInt(hwnd, DWMWA_CLOAKED, out var cloaked, 4) == 0 && cloaked != 0;

    private const int DWMWA_CLOAKED = 14;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int GA_ROOTOWNER = 3;

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeRect
    {
        public int Left, Top, Right, Bottom;
        public readonly int Width => Right - Left;
        public readonly int Height => Bottom - Top;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumWindows(EnumWindowsProc cb, nint lParam);

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsWindowVisible(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int GetWindowTextLength(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "GetWindowTextW", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hwnd, System.Text.StringBuilder text, int maxCount);

    [LibraryImport("user32.dll")]
    private static partial nint GetAncestor(nint hwnd, uint flags);

    [LibraryImport("user32.dll")]
    private static partial nint GetLastActivePopup(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(nint hwnd, out NativeRect rect);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessage(nint hwnd, uint msg, nuint wParam, nint lParam);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BitBlt(nint hdcDest, int x, int y, int w, int h, nint hdcSrc, int xSrc, int ySrc, int rop);

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint hdc);

    private const int SRCCOPY = 0x00CC0020;
    private const int CAPTUREBLT = 0x40000000;

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttributeRect(nint hwnd, int attr, out NativeRect rect, int cbSize);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmGetWindowAttributeInt(nint hwnd, int attr, out int value, int cbSize);
}
