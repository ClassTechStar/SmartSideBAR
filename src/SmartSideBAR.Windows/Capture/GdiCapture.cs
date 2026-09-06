// Windows/Capture/GdiCapture.cs —— §6.2: BitBlt 物理像素直接裁剪 —— 与选框同一坐标系,
// A4 类多屏/DPI 偏移在结构上不可能复现。保存编码走 System.Drawing (进程内, 零子进程)。
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Windows.AppBar;
using SmartSideBAR.Windows.Native;

namespace SmartSideBAR.Windows.Capture;

public interface ICaptureApi
{
    /// <summary>按物理像素矩形截屏 (PMv2 上下文保证坐标物理语义)。</summary>
    Bitmap? CaptureRegion(AppBarRect regionPx);

    /// <summary>截取 hwnd 所在显示器全屏。</summary>
    Bitmap? CaptureMonitor(nint hwnd);

    string SaveImage(Bitmap bmp, string dir, string format);
}

public sealed class GdiCapture(ILogger<GdiCapture>? log = null) : ICaptureApi
{
    public Bitmap? CaptureRegion(AppBarRect regionPx)
    {
        if (regionPx.W <= 0 || regionPx.H <= 0) return null;
        try
        {
            var bmp = new Bitmap(regionPx.W, regionPx.H, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            var hdcDest = g.GetHdc();
            var hdcSrc = NativeCapture.GetDC(0);
            try
            {
                _ = NativeCapture.BitBlt(hdcDest, 0, 0, regionPx.W, regionPx.H,
                    hdcSrc, regionPx.X, regionPx.Y,
                    NativeCapture.SRCCOPY | NativeCapture.CAPTUREBLT);
            }
            finally
            {
                g.ReleaseHdc(hdcDest);
                _ = NativeCapture.ReleaseDC(0, hdcSrc);
            }
            return bmp;
        }
        catch (Exception ex)
        {
            log?.LogError(ex, "[Capture] 区域截图失败 ({X},{Y} {W}x{H})", regionPx.X, regionPx.Y, regionPx.W, regionPx.H);
            return null;
        }
    }

    public Bitmap? CaptureMonitor(nint hwnd)
    {
        using var scope = new AppBarApi.PhysicalDpiScope();
        var mon = AppBarApiProbe.MonitorRectOf(hwnd);
        return CaptureRegion(mon);
    }
    public string SaveImage(Bitmap bmp, string dir, string format)
    {
        Directory.CreateDirectory(dir);
        var ext = format.Equals("jpg", StringComparison.OrdinalIgnoreCase) ? "jpg" : "png";
        var name = $"shot_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}";
        var path = Path.Combine(dir, name);
        var encoder = ext == "jpg" ? ImageFormat.Jpeg : ImageFormat.Png;
        bmp.Save(path, encoder);
        log?.LogInformation("[Capture] 已保存 {Path} ({W}x{H})", path, bmp.Width, bmp.Height);
        return path;
    }
}

/// <summary>AppBarApi 内部 PMv2 作用域的对外复用点 (显示器物理矩形查询)。</summary>
public static class AppBarApiProbe
{
    public static AppBarRect MonitorRectOf(nint hwnd)
    {
        var hMonitor = AppBarNative.MonitorFromWindow(hwnd, AppBarNative.MONITOR_DEFAULTTONEAR);
        var mi = new AppBarNative.MONITORINFO { cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<AppBarNative.MONITORINFO>() };
        if (!AppBarNative.GetMonitorInfo(hMonitor, ref mi))
        {
            return new AppBarRect(0, 0, 1920, 1080);
        }
        return new AppBarRect(mi.rcMonitor.Left, mi.rcMonitor.Top, mi.rcMonitor.W, mi.rcMonitor.H);
    }
}

/// <summary>GDI 位块传输 P/Invoke。</summary>
internal static partial class NativeCapture
{
    public const int SRCCOPY = 0x00CC0020;
    public const int CAPTUREBLT = 0x40000000;

    [LibraryImport("user32.dll")]
    internal static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    internal static partial int ReleaseDC(nint hwnd, nint hdc);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool BitBlt(nint hdcDest, int x, int y, int w, int h, nint hdcSrc, int xSrc, int ySrc, int rop);
}
