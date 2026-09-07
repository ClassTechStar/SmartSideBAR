// Windows/Recording/GraphicsCaptureRecorder.cs —— §6.5 (ADR-M4): 系统级捕获 → H.264 MP4 直出。
// 管线: GraphicsCaptureItem → Direct3D11CaptureFramePool(GPU 纹理零拷贝) → MediaStreamSource
//       → MediaTranscoder(系统 H.264 编码器) → MP4。全 WinRT 投影, 无 COM vtable 手写。
// 注意: file-scoped namespace 下 `Windows.*` 会被解析为 SmartSideBAR.Windows.* —— 一律 global:: 别名。
// config.recorder.fps/bitrate 消费; 麦克风混流列 Wave D 增强 (ADR-M4 最小闭环)。
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SmartSideBAR.Core.Messaging;
using WinRT;
using WinMediaCore = global::Windows.Media.Core;
using WinProps = global::Windows.Media.MediaProperties;
using WinTranscoding = global::Windows.Media.Transcoding;
using WinCapture = global::Windows.Graphics.Capture;
using WinDirectX = global::Windows.Graphics.DirectX;
using WinDirectX11 = global::Windows.Graphics.DirectX.Direct3D11;
using WinGraphics = global::Windows.Graphics;

namespace SmartSideBAR.Windows.Recording;

public sealed record RecorderOptions(string FilePath, int Fps, int BitrateKbps);

public sealed record RecorderStatusChanged(bool Recording, string? FilePath, string? Error);

public interface IRecorderApi
{
    bool IsSupported();
    Task StartAsync(RecorderOptions opt, CancellationToken ct = default);
    Task<string?> StopAsync();
    bool IsRecording { get; }
}

public sealed class GraphicsCaptureRecorder(IEventBus bus, ILogger<GraphicsCaptureRecorder>? log = null)
    : IRecorderApi, IDisposable
{
    private readonly SemaphoreSlim _frameAvailable = new(0);
    private readonly object _gate = new();
    private WinCapture.Direct3D11CaptureFramePool? _framePool;
    private WinCapture.GraphicsCaptureSession? _session;
    private WinCapture.Direct3D11CaptureFrame? _pending;
    private WinMediaCore.MediaStreamSource? _mss;
    private WinTranscoding.PrepareTranscodeResult? _prepared;
    private string? _filePath;
    private volatile bool _recording;
    private volatile bool _stopRequested;
    private int _fps = 15;

    public bool IsRecording => _recording;

    public bool IsSupported()
    {
        try { return WinCapture.GraphicsCaptureSession.IsSupported(); }
        catch { return false; }
    }

    public async Task StartAsync(RecorderOptions opt, CancellationToken ct = default)
    {
        if (_recording) throw new InvalidOperationException("已在录制中");
        if (!IsSupported()) throw new PlatformNotSupportedException("系统不支持 Windows.Graphics.Capture (需 Win10 1803+)");

        // 主屏物理尺寸 (PMv2 上下文; TFM 已含 WinRT 投影)
        var hMonitor = GraphicsCaptureItemProbe.PrimaryMonitorHandle();
        var item = GraphicsCaptureItemProbe.CreateForMonitor(hMonitor);
        var size = item.Size;
        _filePath = opt.FilePath;
        _fps = Math.Clamp(opt.Fps, 5, 60);

        var device = D3D11Helper.CreateDevice();
        _framePool = WinCapture.Direct3D11CaptureFramePool.CreateFreeThreaded(
            device, WinDirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);

        // MP4 容器 + H.264 视频轨 (无音频轨; mic 混流为 Wave D 增强)
        var profile = WinProps.MediaEncodingProfile.CreateMp4(
            WinProps.VideoEncodingQuality.HD720p);
        profile.Video = WinProps.VideoEncodingProperties.CreateH264();
        profile.Video.Width = (uint)size.Width;
        profile.Video.Height = (uint)size.Height;
        profile.Video.Bitrate = (uint)Math.Max(500_000, opt.BitrateKbps * 1000);
        profile.Video.FrameRate.Numerator = (uint)_fps;
        profile.Video.FrameRate.Denominator = 1;
        profile.Audio = null;

        var props = WinProps.VideoEncodingProperties.CreateUncompressed(
            WinProps.MediaEncodingSubtypes.Rgb32, (uint)size.Width, (uint)size.Height);
        _mss = new WinMediaCore.MediaStreamSource(new WinMediaCore.VideoStreamDescriptor(props))
        {
            BufferTime = TimeSpan.Zero,
        };
        _mss.Starting += OnStarting;
        _mss.SampleRequested += OnSampleRequested;

        var transcoder = new WinTranscoding.MediaTranscoder { HardwareAccelerationEnabled = true };
        _pending = null;
        _stopRequested = false;
        _recording = true;
        _session = _framePool.CreateCaptureSession(item);
        _session.IsCursorCaptureEnabled = true;
        _framePool.FrameArrived += OnFrameArrived;
        _session.StartCapture();

        // 转码泵: SampleRequested 阻塞取帧, StopAsync 交付 null 使其自然收尾 (MP4 moov 完整)
        File.Create(opt.FilePath).Dispose(); // 先建文件供 WinRT StorageFile 打开
        var storageFile = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(opt.FilePath).AsTask(ct).ConfigureAwait(false);
        var stream = await storageFile.OpenAsync(global::Windows.Storage.FileAccessMode.ReadWrite).AsTask(ct).ConfigureAwait(false);
        var prepared = await transcoder.PrepareMediaStreamSourceTranscodeAsync(_mss, stream, profile).AsTask(ct).ConfigureAwait(false);
        _prepared = prepared;
        _ = Task.Run(async () =>
        {
            try { await prepared.TranscodeAsync().AsTask().ConfigureAwait(false); }
            catch (Exception ex) { log?.LogWarning(ex, "[Recorder] 转码泵退出"); }
        }, CancellationToken.None);

        bus.Publish(new RecorderStatusChanged(true, opt.FilePath, null));
        log?.LogInformation("[Recorder] 开始 {Path} ({Fps}fps/{Bitrate}kbps {W}x{H})",
            opt.FilePath, _fps, opt.BitrateKbps, size.Width, size.Height);
    }

    public async Task<string?> StopAsync()
    {
        if (!_recording) return null;
        _stopRequested = true;
        _frameAvailable.Release(); // 唤醒阻塞的取帧点 → 交付 null → 转码收尾
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(6);
        while (_recording && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100).ConfigureAwait(false);
        }
        Cleanup();
        bus.Publish(new RecorderStatusChanged(false, _filePath, null));
        log?.LogInformation("[Recorder] 完成 {Path}", _filePath);
        return _filePath;
    }

    private void OnFrameArrived(WinCapture.Direct3D11CaptureFramePool sender, object args)
    {
        var frame = sender.TryGetNextFrame();
        if (frame is null) return;
        lock (_gate)
        {
            _pending?.Dispose();
            _pending = frame;
        }
        _ = _frameAvailable.Release();
    }

    private void OnStarting(WinMediaCore.MediaStreamSource sender, WinMediaCore.MediaStreamSourceStartingEventArgs args)
    {
        args.Request.SetActualStartPosition(TimeSpan.Zero);
    }

    private void OnSampleRequested(WinMediaCore.MediaStreamSource sender, WinMediaCore.MediaStreamSourceSampleRequestedEventArgs args)
    {
        if (_stopRequested)
        {
            args.Request.Sample = null; // 收尾信号 → TranscodeAsync 完成
            _recording = false;
            return;
        }
        try
        {
            _frameAvailable.Wait(TimeSpan.FromSeconds(3));
            lock (_gate)
            {
                var frame = _pending;
                _pending = null;
                if (frame is null)
                {
                    args.Request.Sample = null;
                    _recording = false;
                    return;
                }
                var sample = WinMediaCore.MediaStreamSample.CreateFromDirect3D11Surface(
                    frame.Surface, frame.SystemRelativeTime);
                sample.Duration = TimeSpan.FromMilliseconds(1000.0 / _fps);
                args.Request.Sample = sample;
                frame.Dispose();
            }
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "[Recorder] 取帧失败, 提前收尾");
            args.Request.Sample = null;
            _recording = false;
        }
    }

    private void Cleanup()
    {
        try
        {
            if (_mss is { } mss)
            {
                mss.Starting -= OnStarting;
                mss.SampleRequested -= OnSampleRequested;
            }
            if (_framePool is { } pool) pool.FrameArrived -= OnFrameArrived;
            _session?.Dispose();
            _framePool?.Dispose();
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "[Recorder] 清理异常");
        }
        _session = null;
        _framePool = null;
        _mss = null;
        _prepared = null;
    }

    public void Dispose() => _ = StopAsync();
}

/// <summary>D3D11 设备创建与 WinRT IDirect3DDevice 桥 (官方互操作路径)。</summary>
internal static class D3D11Helper
{
    private const uint D3D11_SDK_VERSION = 7;
    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const int D3D11_CREATE_DEVICE_BGRA_SUPPORT = 0x20;
    private static readonly Guid IID_IDXGIDevice = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");

    [DllImport("d3d11.dll", ExactSpelling = true, SetLastError = false)]
    private static extern int D3D11CreateDevice(
        nint pAdapter, int driverType, uint software, uint flags,
        nint pFeatureLevels, uint numFeatureLevels, uint sdkVersion,
        out nint ppDevice, out uint featureLevel, out nint ppImmediateContext);

    [DllImport("d3d11.dll", EntryPoint = "CreateDirect3D11DeviceFromDXGIDevice", ExactSpelling = true, SetLastError = false)]
    private static extern int CreateDirect3D11DeviceFromDXGIDevice(nint dxgiDevice, out nint graphicsDevice);

    public static WinDirectX11.IDirect3DDevice CreateDevice()
    {
        var hr = D3D11CreateDevice(0, D3D_DRIVER_TYPE_HARDWARE, 0, D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            0, 0, D3D11_SDK_VERSION, out var d3dDevice, out var featureLevel, out var context);
        Marshal.ThrowExceptionForHR(hr);
        try
        {
            var iid = IID_IDXGIDevice;
            hr = Marshal.QueryInterface(d3dDevice, in iid, out var dxgiDevice);
            Marshal.ThrowExceptionForHR(hr);
            try
            {
                hr = CreateDirect3D11DeviceFromDXGIDevice(dxgiDevice, out var inspectable);
                Marshal.ThrowExceptionForHR(hr);
                try
                {
                    return MarshalInterface<WinDirectX11.IDirect3DDevice>.FromAbi(inspectable);
                }
                finally { Marshal.Release(inspectable); }
            }
            finally { Marshal.Release(dxgiDevice); }
        }
        finally
        {
            Marshal.Release(d3dDevice);
            if (context != 0) Marshal.Release(context);
        }
    }
}

/// <summary>主显示器 HMONITOR → GraphicsCaptureItem。
/// 19041 投影的 GraphicsCaptureItem 仅有 CreateFromVisual —— 监视器捕获必须走
/// IGraphicsCaptureItemInterop COM 互操作 (官方 CsWinRT 采样同路径)。</summary>
internal static partial class GraphicsCaptureItemProbe
{
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    public static long PrimaryMonitorHandle()
    {
        var hmon = MonitorFromPoint(new NativePoint { x = 0, y = 0 }, 1 /*MONITOR_DEFAULTTOPRIMARY*/);
        return hmon;
    }

    public static WinCapture.GraphicsCaptureItem CreateForMonitor(long hMonitor)
    {
        var factory = global::WinRT.ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        var interop = factory.AsInterface<IGraphicsCaptureItemInterop>();
        var iid = GraphicsCaptureItemIid;
        var ptr = interop.CreateForMonitor((nint)hMonitor, in iid);
        try
        {
            return WinCapture.GraphicsCaptureItem.FromAbi(ptr);
        }
        finally
        {
            Marshal.Release(ptr);
        }
    }

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        nint CreateForWindow(nint window, in Guid iid);

        nint CreateForMonitor(nint hmonitor, in Guid iid);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativePoint { public int x, y; }

    [LibraryImport("user32.dll")]
    private static partial nint MonitorFromPoint(NativePoint pt, uint flags);
}
