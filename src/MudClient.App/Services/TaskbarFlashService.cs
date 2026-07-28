using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;

namespace MudClient.App.Services;

/// <summary>
/// Flashes the taskbar icon via the Win32 <c>FlashWindowEx</c> API — mirrors the community
/// Mudlet package's <c>alert(5)</c> call for this MUD's chat notifications. A no-op on any
/// non-Windows platform (the app is otherwise cross-platform; there is no equivalent Avalonia API
/// and no other platform-specific code exists in this project to extend).
/// </summary>
public static class TaskbarFlashService
{
    private const uint FlashwTray = 2;
    private const uint FlashwTimernofg = 12;
    private const uint FlashwStop = 0;

    /// <summary>Starts flashing the taskbar icon until <see cref="Stop"/> is called or the
    /// window is brought to the foreground.</summary>
    public static void Start(Window window)
    {
        if (OperatingSystem.IsWindows())
        {
            StartCore(window);
        }
    }

    /// <summary>Cancels any in-progress flash — called once a 5-second timer elapses, or
    /// immediately if the window regains focus first.</summary>
    public static void Stop(Window window)
    {
        if (OperatingSystem.IsWindows())
        {
            StopCore(window);
        }
    }

    [SupportedOSPlatform("windows")]
    private static void StartCore(Window window)
    {
        if (GetHandle(window) is not { } handle)
        {
            return;
        }

        var info = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = handle,
            dwFlags = FlashwTray | FlashwTimernofg,
            uCount = uint.MaxValue,
            dwTimeout = 0,
        };
        FlashWindowEx(ref info);
    }

    [SupportedOSPlatform("windows")]
    private static void StopCore(Window window)
    {
        if (GetHandle(window) is not { } handle)
        {
            return;
        }

        var info = new FLASHWINFO
        {
            cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
            hwnd = handle,
            dwFlags = FlashwStop,
            uCount = 0,
            dwTimeout = 0,
        };
        FlashWindowEx(ref info);
    }

    private static IntPtr? GetHandle(Window window)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        return handle == IntPtr.Zero ? null : handle;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    [SupportedOSPlatform("windows")]
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);
}
