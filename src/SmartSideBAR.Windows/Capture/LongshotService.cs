// Windows/Capture/LongshotService.cs —— §6.4 长截图: EnumWindows 定位 + BitBlt 滚动帧 + 线性拼接。
// 跨屏/DPI 用 DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS) 物理矩形 (A5 根修)。
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

            Bitmap canvas;
            using (var scope = new AppBarApi.PhysicalDpiScope())
            {
                var first = CaptureRect(rect);
                canvas = first ?? throw new InvalidOperationException("首帧捕获失败");
            }

            var frames = 1;
            var committed = canvas.Height;
            while (frames < MaxFrames && committed < MaxHeightPx)
            {
                ct.ThrowIfCancellationRequested();
                _ = PostMessage(target.Hwnd, WmKeyDown, VkNext, 0);
                _ = PostMessage(target.Hwnd, WmKeyUp, VkNext, 0);
                await Task.Delay(ScrollWaitMs, ct).ConfigureAwait(false);

                Bitmap frame;
                using (var scope = new AppBarApi.PhysicalDpiScope())
                {
                    var captured = CaptureRect(rect);
                    if (captured is null) break;
                    frame = captured;
                }

                var tailHashes = RowHashes(canvas, Math.Min(canvas.Height, frame.Height));
                var frameHashes = RowHashes(frame, frame.Height);
                var overlap = LongshotStitcher.FindOverlap(tailHashes, frameHashes, Math.Min(tailHashes.Length, frame.Height));
                if (overlap >= frame.Height)
                {
                    frame.Dispose();
                    break; // 已到底
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

    /// <summary>行哈希: 逐行采样求和 (快而稳; 完全一致才计重叠)。</summary>
    internal static long[] RowHashes(Bitmap bmp, int rowCount)
    {
        var hashes = new long[rowCount];
        var rect = new Rectangle(0, 0, bmp.Width, Math.Min(rowCount, bmp.Height));
        var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        unsafe
        {
            var stride = data.Stride;
            var basePtr = (byte*)data.Scan0;
            var step = Math.Max(1, bmp.Width / 64); // 每行最多采样 64 点
            for (var y = 0; y < rowCount; y++)
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
