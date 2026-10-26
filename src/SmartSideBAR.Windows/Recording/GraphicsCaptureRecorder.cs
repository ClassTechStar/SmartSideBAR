// §6.5 (ADR-M4): 系统级捕获 → H.264 MP4 直出。
// 管线: GraphicsCaptureItem → Direct3D11CaptureFramePool(GPU 纹理零拷贝) → MediaStreamSource
//       → MediaTranscoder(系统 H.264 编码器) → MP4。全 WinRT 投影, 无 COM vtable 手写。
// 注意: file-scoped namespace 下 `Windows.*` 会被解析为 SmartSideBAR.Windows.*, 一律 global:: 别名。
// config.recorder.fps/bitrate 消费; 麦克风混流列 Wave D 增强 (ADR-M4 最小完整流程)。
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

public sealed record RecorderOptions(string FilePath, int Fps, int BitrateKbps, long MonitorHandle = 0, bool Microphone = false);

public sealed record RecorderStatusChanged(bool Recording, string? FilePath, string? Error);

public interface IRecorderApi
{
    bool IsSupported();
    Task StartAsync(RecorderOptions opt, CancellationToken ct = default);
    Task<string?> StopAsync();
    bool IsRecording { get; }

    /// <summary>本次录屏的开始时刻 (本地时间); 未录制时为 null。面板据此显示计时。</summary>
    DateTimeOffset? StartedAt { get; }
}

public sealed class GraphicsCaptureRecorder(IEventBus bus, ILogger<GraphicsCaptureRecorder>? log = null)
    : IRecorderApi, IDisposable
{
    private DateTimeOffset? _startedAt;
    private readonly SemaphoreSlim _frameAvailable = new(0);
    private readonly object _gate = new();
    private WinCapture.Direct3D11CaptureFramePool? _framePool;
    private WinCapture.GraphicsCaptureSession? _session;
    private WinCapture.Direct3D11CaptureFrame? _pending;
    private WinCapture.Direct3D11CaptureFrame? _lastFrame;
    private WinMediaCore.MediaStreamSource? _mss;
    private WinTranscoding.PrepareTranscodeResult? _prepared;
    private string? _filePath;
    private volatile bool _recording;
    private volatile bool _stopRequested;
    private int _fps = 15;
    private global::Windows.Storage.Streams.IRandomAccessStream? _output;
    private Task? _pump;
    private int _framesArrived;      // 采集回调送到的帧数
    private int _samplesDelivered;   // 真正交给转码器的采样数
    private int _waitTimeouts;       // 取帧等待超时次数
    private int _nullSamples;        // 交付 null（结束信号）次数

    public DateTimeOffset? StartedAt => _startedAt;

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

                // B5: 多显示器支持, 优先用指定 MonitorHandle, 否则默认主屏
        var hMonitor = opt.MonitorHandle != 0
            ? opt.MonitorHandle
            : GraphicsCaptureItemProbe.PrimaryMonitorHandle();
        var item = GraphicsCaptureItemProbe.CreateForMonitor(hMonitor);
        var size = item.Size;
        _filePath = opt.FilePath;
        _fps = Math.Clamp(opt.Fps, 5, 60);

        var device = D3D11Helper.CreateDevice();
        _framePool = WinCapture.Direct3D11CaptureFramePool.CreateFreeThreaded(
            device, WinDirectX.DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, size);

        // MP4 容器 + H.264 视频轨 + 可选音频轨 (B5: 麦克风混流)
        var profile = WinProps.MediaEncodingProfile.CreateMp4(
            WinProps.VideoEncodingQuality.HD720p);
        profile.Video = WinProps.VideoEncodingProperties.CreateH264();
        profile.Video.Width = (uint)size.Width;
        profile.Video.Height = (uint)size.Height;
        profile.Video.Bitrate = (uint)Math.Max(500_000, opt.BitrateKbps * 1000);
        profile.Video.FrameRate.Numerator = (uint)_fps;
        profile.Video.FrameRate.Denominator = 1;

                // B9: 不能只声明 AAC 音频轨而不给音频采样源。转码器为音频流索取 sample 时
        // OnSampleRequested 只能返回视频帧或 null, 音频流立即 EOF, 整段转码提前结束
        // (录屏变成 0 字节或毫秒级文件)。本版本统一走「纯视频」, 麦克风偏好仅落配置。
        if (opt.Microphone)
        {
            log?.LogWarning("[Recorder] 麦克风混流尚未实现 (无音频采样源), 本次仅录制画面");
        }
        profile.Audio = null;

        // 必须与帧池像素格式一致：Direct3D11CaptureFramePool 建的是
                // B8G8R8A8UIntNormalized (BGRA)。若声明成 Rgb32, 与真实采样格式不符,
        // 转码器不认这个流 → 从不发起 SampleRequested → 只收到首帧后彻底停滞，
        // 最终产出 0 字节（并在收尾阶段把进程带崩）。
        var videoProps = WinProps.VideoEncodingProperties.CreateUncompressed(
            WinProps.MediaEncodingSubtypes.Bgra8, (uint)size.Width, (uint)size.Height);
        _mss = new WinMediaCore.MediaStreamSource(new WinMediaCore.VideoStreamDescriptor(videoProps))
        {
            BufferTime = TimeSpan.Zero,
        };
        _mss.Starting += OnStarting;
        _mss.SampleRequested += OnSampleRequested;

                // 注意: 不能在 StartAsync 作用域内 using 释放, PrepareTranscodeResult 仍在使用该转码器
        // GPU 硬编码在部分机型（如骁龙 ARM 上的 x64 仿真）会让 MediaTranscoder 直接把进程带崩，
                // 且没有任何托管异常, 表现为录屏文件 0 字节。默认走软件编码器求稳。
        var transcoder = new WinTranscoding.MediaTranscoder { HardwareAccelerationEnabled = false };
        _pending = null;
        _stopRequested = false;
        _recording = true;
        _startedAt = DateTimeOffset.Now;
        _session = _framePool.CreateCaptureSession(item);
        _session.IsCursorCaptureEnabled = true;
        _framePool.FrameArrived += OnFrameArrived;
        _session.StartCapture();

        // 转码泵: SampleRequested 阻塞取帧, StopAsync 交付 null 使其自然收尾 (MP4 moov 完整)
        // 关键保底：本机的 MediaFoundation 管道（骁龙 ARM 上的 x64 仿真）可能让
        // PrepareMediaStreamSourceTranscodeAsync 永不返回，甚至直接把进程带崩。
        // 这里给它一个硬超时：超时就干净收尾并上报错误，绝不留下"一直在录"的假状态。
        async Task<WinTranscoding.PrepareTranscodeResult> PrepareAsync()
        {
            File.Create(opt.FilePath).Dispose(); // 先建文件供 WinRT StorageFile 打开
            var storageFile = await global::Windows.Storage.StorageFile.GetFileFromPathAsync(opt.FilePath).AsTask().ConfigureAwait(false);
            var stream = await storageFile.OpenAsync(global::Windows.Storage.FileAccessMode.ReadWrite).AsTask().ConfigureAwait(false);
            _output = stream;
            return await transcoder.PrepareMediaStreamSourceTranscodeAsync(_mss, stream, profile).AsTask().ConfigureAwait(false);
        }

        var prepareTask = PrepareAsync();
        var timeout = Task.Delay(TimeSpan.FromSeconds(10));
        var winner = await Task.WhenAny(prepareTask, timeout).ConfigureAwait(false);
        if (winner != prepareTask)
        {
            log?.LogError("[Recorder] 转码器准备超时 10s —— 本机媒体管道不可用, 已放弃本次录制");
            _recording = false;
            _startedAt = null;
            Cleanup();
            bus.Publish(new RecorderStatusChanged(false, null, "转码器准备超时（本机媒体管道不可用）"));
            throw new TimeoutException("MediaTranscoder 准备超时（本机媒体管道不可用）");
        }

        var prepared = await prepareTask.ConfigureAwait(false);
        _prepared = prepared;
        _pump = Task.Run(async () =>
        {
            try
            {
                await prepared.TranscodeAsync().AsTask().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log?.LogWarning(ex, "[Recorder] 转码泵异常退出");
            }
            finally
            {
                // 关键：TranscodeAsync 返回后必须 flush/关闭输出流，MP4 的 moov 才会真正落盘。
                                // 完成信号不能早于落盘, 否则日志报完成但文件 0 字节。
                try
                {
                    if (_output is { } output)
                    {
                        await output.FlushAsync().AsTask().ConfigureAwait(false);
                        output.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    log?.LogWarning(ex, "[Recorder] 输出流收尾失败");
                }
                _output = null;
                _recording = false;
                _startedAt = null;
                var size = File.Exists(_filePath) ? new FileInfo(_filePath).Length : 0;
                log?.LogInformation(
                    "[Recorder] 转码结束: 到达帧={Arrived} 交付采样={Delivered} 超时={Timeouts} 结束信号={Nulls} 文件大小={Size}B",
                    _framesArrived, _samplesDelivered, _waitTimeouts, _nullSamples, size);
                if (size == 0)
                {
                    log?.LogWarning("[Recorder] 产出 0 字节 —— 通常为采集端没送帧或采样立即 EOF");
                }
                bus.Publish(new RecorderStatusChanged(false, _filePath, null));
            }
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

                // 等转码泵真正跑完 (含输出流 flush)。只等 _recording 变 false 的话,
                // 完成日志会先落、文件后写, 结果就是 0 字节。
        try
        {
            var pump = _pump;
            if (pump is not null)
            {
                var finished = await Task.WhenAny(pump, Task.Delay(TimeSpan.FromSeconds(15))).ConfigureAwait(false);
                if (finished != pump) log?.LogWarning("[Recorder] 转码泵 15s 内未结束, 放弃等待");
            }
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "[Recorder] 等待转码收尾失败");
        }

        Cleanup();
        var bytes = _filePath is { } path && File.Exists(path) ? new FileInfo(path).Length : 0;
        log?.LogInformation("[Recorder] 完成 {Path} ({Bytes} 字节)", _filePath, bytes);
        _pump = null;
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
        Interlocked.Increment(ref _framesArrived);
        if (_framesArrived == 1) log?.LogInformation("[Recorder] 首帧到达");
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
            Interlocked.Increment(ref _nullSamples);
            args.Request.Sample = null; // 收尾信号 → TranscodeAsync 完成
            return;
        }
        try
        {
            // 首次取帧给足耐心：冷启动（D3D 设备/采集会话初始化）可能超过 1 个等待周期。
                        // 单次 3s 超时就交付 null 相当于刚开始就 EOF, 文件必然是空的。
            const int maxAttempts = 3;
            var attempt = 0;
            while (attempt++ < maxAttempts)
            {
                if (_frameAvailable.Wait(TimeSpan.FromSeconds(3))) break;
                Interlocked.Increment(ref _waitTimeouts);
                if (_stopRequested) break;
                log?.LogWarning("[Recorder] 等待采集帧超时 ({Attempt}/{Max})", attempt, maxAttempts);
            }

            lock (_gate)
            {
                var frame = _pending;
                _pending = null;
                if (frame is null)
                {
                    Interlocked.Increment(ref _nullSamples);
                    log?.LogWarning("[Recorder] 无帧可交付, 触发结束信号 (到达帧={Arrived})", _framesArrived);
                    args.Request.Sample = null;
                    return;
                }

                var sample = WinMediaCore.MediaStreamSample.CreateFromDirect3D11Surface(
                    frame.Surface, frame.SystemRelativeTime);
                sample.Duration = TimeSpan.FromMilliseconds(1000.0 / _fps);
                args.Request.Sample = sample;
                Interlocked.Increment(ref _samplesDelivered);
                if (_samplesDelivered <= 3)
                {
                    log?.LogInformation("[Recorder] 交付采样 #{N} (时长 {Ms}ms)", _samplesDelivered, 1000.0 / _fps);
                }
                // 保留上一帧到下一次采样再释放：立即 Dispose 会让转码器读到已释放的 surface
                _lastFrame?.Dispose();
                _lastFrame = frame;
            }
        }
        catch (Exception ex)
        {
            log?.LogWarning(ex, "[Recorder] 取帧失败, 提前收尾");
            args.Request.Sample = null;
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
            lock (_gate)
            {
                _pending?.Dispose();
                _pending = null;
                _lastFrame?.Dispose();
                _lastFrame = null;
            }
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
/// 19041 投影的 GraphicsCaptureItem 仅有 CreateFromVisual, 监视器捕获必须走
/// IGraphicsCaptureItemInterop COM 互操作 (官方 CsWinRT 采样同路径)。</summary>
internal static partial class GraphicsCaptureItemProbe
{
    private static readonly Guid GraphicsCaptureItemIid = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    public static long PrimaryMonitorHandle()
    {
        var hmon = MonitorFromPoint(new NativePoint { x = 0, y = 0 }, 1 /*MONITOR_DEFAULTTOPRIMARY*/);
        return hmon;
    }

    /// <summary>B5: 枚举所有显示器句柄, 供 UI 选屏。</summary>
    public static List<long> EnumerateMonitorHandles()
    {
        var handles = new List<long>();
        EnumDisplayMonitors(0, 0, (hmon, _, _, _) =>
        {
            handles.Add(hmon);
            return true;
        }, 0);
        return handles;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EnumDisplayMonitors(
        nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

    private delegate bool MonitorEnumProc(nint hMonitor, nint hdc, nint lprc, nint dwData);

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
