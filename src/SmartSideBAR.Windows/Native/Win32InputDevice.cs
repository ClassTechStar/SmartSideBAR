// Windows/Native/Win32InputDevice.cs —— Wave B 服务族共享的 P/Invoke 集合
// (IME 键盘布局 / 全局热键 / 设备通知 / winmm 声音)
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace SmartSideBAR.Windows.Native;

internal static partial class Win32Input
{
    // ---- 前台窗口 / 线程键盘布局 ----
    [LibraryImport("user32.dll")]
    internal static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    internal static partial uint GetWindowThreadProcessId(nint hwnd, out uint lpdwProcessId);

    [LibraryImport("user32.dll")]
    internal static partial nint GetKeyboardLayout(uint idThread);

    [LibraryImport("user32.dll")]
    private static partial uint GetKeyboardLayoutList(int nBuff, [Out] nint[] lpList);

    internal static nint[] GetKeyboardLayouts()
    {
        var n = (int)GetKeyboardLayoutList(0, []);
        if (n <= 0) return [];
        var buf = new nint[n];
        _ = GetKeyboardLayoutList(n, buf);
        return buf;
    }

    [LibraryImport("user32.dll", EntryPoint = "LoadKeyboardLayoutW", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint LoadKeyboardLayout(string pwszKLID, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "PostMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PostMessage(nint hwnd, uint msg, nuint wParam, nint lParam);

    public const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
    public const uint KLF_ACTIVATE = 0x0001;

    // ---- 全局热键 ----
    public const uint WM_HOTKEY = 0x0312;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint hWnd, int id);

    // ---- 设备通知 (USB) ----
    public const uint WM_DEVICECHANGE = 0x0219;
    public const uint DBT_DEVICEARRIVAL = 0x8000;
    public const uint DBT_DEVICEREMOVECOMPLETE = 0x8004;
    public const uint DBT_DEVTYP_VOLUME = 0x0002;
    public const uint DEVICE_NOTIFY_WINDOW_HANDLE = 0x0000;

    [StructLayout(LayoutKind.Sequential)]
    public struct DEV_BROADCAST_VOLUME
    {
        public uint dbcv_size;
        public uint dbcv_devicetype;
        public uint dbcv_reserved;
        public uint dbcv_unitmask;
        public ushort dbcv_flags;
    }

    [DllImport("user32.dll", EntryPoint = "RegisterDeviceNotificationW", SetLastError = true)]
    internal static extern nint RegisterDeviceNotification(nint hRecipient, ref DEV_BROADCAST_VOLUME notification, uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern bool UnregisterDeviceNotification(nint handle);
}

/// <summary>winmm 声音播放 (零子进程; 替代 v1.2.0 的 PowerShell SystemSounds 守护)。</summary>
internal static partial class Winmm
{
    public const uint SND_ALIAS = 0x00010000;
    public const uint SND_ASYNC = 0x0001;

    [LibraryImport("winmm.dll", EntryPoint = "PlaySoundW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool PlaySound(string? pszSound, nint hmod, uint fdwSound);

    [LibraryImport("winmm.dll", EntryPoint = "mciSendStringW", StringMarshalling = StringMarshalling.Utf16,
        SetLastError = true)]
    internal static partial int mciSendString(string lpstrCommand, char[]? lpstrReturnString, int cchReturn, nint hwndCallback);
}
