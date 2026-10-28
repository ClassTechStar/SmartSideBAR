// 系统主音量调节 (P1-4 的一部分)。
// 借鉴 DiffiTouch 的 sound_page：课堂场景常用。
// 走 Core Audio 的 IAudioEndpointVolume；COM 不可用或 trimmed 环境下探测失败，
// 由 IsSupported 返回 false，调用方隐藏滑杆而不是摆一个点了没反应的控件。
using System.Runtime.InteropServices;

namespace SmartSideBAR.Windows.Audio;

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorComObject { }

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);
    int GetDefaultAudioEndpoint(int dataFlow, int role, out nint endpoint);
    int GetDevice(string id, out nint device);
    int RegisterEndpointNotificationCallback(nint client);
    int UnregisterEndpointNotificationCallback(nint client);
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    int RegisterControlChangeNotify(nint notify);
    int UnregisterControlChangeNotify(nint notify);
    int GetChannelCount(out int count);
    int SetMasterVolumeLevel(float level, nint context);
    int SetMasterVolumeLevelScalar(float level, nint context);
    int GetMasterVolumeLevel(out float level);
    int GetMasterVolumeLevelScalar(out float level);
    int SetChannelVolumeLevel(int channel, float level, nint context);
    int SetChannelVolumeLevelScalar(int channel, float level, nint context);
    int GetChannelVolumeLevel(int channel, out float level);
    int GetChannelVolumeLevelScalar(int channel, out float level);
    int SetMute(int mute, nint context);
    int GetMute(out int mute);
    int GetVolumeStepInfo(out int step, out int stepCount);
    int VolumeStepUp(nint context);
    int VolumeStepDown(nint context);
    int QueryHardwareSupport(out int mask);
    int GetVolumeRange(out float min, out float max, out float increment);
}

public sealed class VolumeService
{
    private const int eRender = 0;
    private const int eConsole = 0;
    private bool? _supported;

    /// <summary>Core Audio 是否可用（探测结果缓存）。</summary>
    public bool IsSupported
    {
        get
        {
            _supported ??= Probe();
            return _supported.Value;
        }
    }

    private static bool Probe()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            var hr = enumerator.GetDefaultAudioEndpoint(eRender, eConsole, out var endpoint);
            if (hr != 0 || endpoint == 0) return false;
            _ = Marshal.QueryInterface(endpoint, ref _iid, out var volumePtr);
            if (volumePtr == 0) return false;
            _ = Marshal.Release(volumePtr);
            _ = Marshal.Release(endpoint);
            return true;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static Guid _iid = new("5CDF2C82-841E-4546-9722-0CF74078229A");

    /// <summary>读取主音量百分比（0–100）；不可用返回 null。</summary>
    public int? Get()
    {
        var volume = Acquire();
        if (volume is null) return null;
        try
        {
            var hr = volume.GetMasterVolumeLevelScalar(out var level);
            return hr == 0 ? (int)Math.Round(Math.Clamp(level, 0f, 1f) * 100) : null;
        }
        catch
        {
            return null;
        }
        finally
        {
            Release(volume);
        }
    }

    /// <summary>设置主音量百分比（0–100）。不可用返回 false。</summary>
    public bool Set(int percent)
    {
        var volume = Acquire();
        if (volume is null) return false;
        try
        {
            var scalar = Math.Clamp(percent / 100f, 0f, 1f);
            return volume.SetMasterVolumeLevelScalar(scalar, nint.Zero) == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            Release(volume);
        }
    }

    /// <summary>读取静音状态；不可用返回 null。</summary>
    public bool? GetMute()
    {
        var volume = Acquire();
        if (volume is null) return null;
        try
        {
            return volume.GetMute(out var mute) == 0 && mute != 0;
        }
        catch
        {
            return null;
        }
        finally
        {
            Release(volume);
        }
    }

    /// <summary>切换静音；不可用返回 false。</summary>
    public bool SetMute(bool mute)
    {
        var volume = Acquire();
        if (volume is null) return false;
        try
        {
            return volume.SetMute(mute ? 1 : 0, nint.Zero) == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            Release(volume);
        }
    }

    private static IAudioEndpointVolume? Acquire()
    {
        if (!Probe()) return null;
        try
        {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            if (enumerator.GetDefaultAudioEndpoint(eRender, eConsole, out var endpoint) != 0 || endpoint == 0)
                return null;
            var hr = Marshal.QueryInterface(endpoint, ref _iid, out var volumePtr);
            _ = Marshal.Release(endpoint);
            if (hr != 0 || volumePtr == 0) return null;
            return (IAudioEndpointVolume)Marshal.GetObjectForIUnknown(volumePtr);
        }
        catch
        {
            return null;
        }
    }

    private static void Release(IAudioEndpointVolume volume)
    {
        try
        {
            if (Marshal.IsComObject(volume)) Marshal.ReleaseComObject(volume);
        }
        catch
        {
            // 释放失败不影响调用方
        }
    }
}
