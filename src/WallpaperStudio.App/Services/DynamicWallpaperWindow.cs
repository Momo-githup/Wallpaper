using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace WallpaperStudio.App.Services;

internal sealed class DynamicWallpaperWindow : Window
{
    private readonly MediaElement _player;
    private readonly TaskCompletionSource _opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IntPtr _desktopHost;

    public DynamicWallpaperWindow(string path)
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Background = System.Windows.Media.Brushes.Black;
        _player = new MediaElement
        {
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Manual,
            Stretch = Stretch.UniformToFill,
            Volume = 0,
            Source = new Uri(path, UriKind.Absolute)
        };
        _player.Loaded += (_, _) => _player.Play();
        _player.MediaOpened += (_, _) => _opened.TrySetResult();
        _player.MediaEnded += (_, _) => { _player.Position = TimeSpan.Zero; _player.Play(); };
        _player.MediaFailed += (_, args) => _opened.TrySetException(
            args.ErrorException ?? new InvalidOperationException("Windows 无法播放这个视频。"));
        Content = _player;
        SourceInitialized += OnSourceInitialized;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public async Task WaitUntilOpenedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _opened.Task.WaitAsync(TimeSpan.FromSeconds(12), cancellationToken);
        }
        catch (TimeoutException exception)
        {
            throw new InvalidOperationException("视频加载超时，请尝试 MP4（H.264）格式。", exception);
        }
    }

    public void StopPlayback()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _player.Stop();
        _player.Source = null;
        Close();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        _desktopHost = DesktopHost.Find();
        if (_desktopHost == IntPtr.Zero) throw new InvalidOperationException("无法连接 Windows 桌面窗口。");

        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GwlStyle).ToInt64();
        style = (style & ~NativeMethods.WsPopup) | NativeMethods.WsChild | NativeMethods.WsVisible;
        Marshal.SetLastPInvokeError(0);
        if (NativeMethods.SetParent(handle, _desktopHost) == IntPtr.Zero && Marshal.GetLastPInvokeError() != 0)
            throw new InvalidOperationException("无法把视频窗口挂载到 Windows 桌面。");
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GwlStyle, new IntPtr(style));
        ResizeToDesktop(handle);
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() => ResizeToDesktop(new WindowInteropHelper(this).Handle));
    }

    private void ResizeToDesktop(IntPtr handle)
    {
        if (_desktopHost == IntPtr.Zero || !NativeMethods.GetClientRect(_desktopHost, out var rect)) return;
        NativeMethods.SetWindowPos(handle, new IntPtr(1), 0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow | NativeMethods.SwpFrameChanged);
    }
}

internal static class DesktopHost
{
    public static IntPtr Find()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return IntPtr.Zero;

        NativeMethods.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
        var iconHost = IntPtr.Zero;
        NativeMethods.EnumWindows((top, _) =>
        {
            var shellView = NativeMethods.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView == IntPtr.Zero) return true;
            iconHost = top;
            return false;
        }, IntPtr.Zero);
        return iconHost != IntPtr.Zero ? iconHost : progman;
    }
}

internal static class NativeMethods
{
    internal const int GwlStyle = -16;
    internal const long WsChild = 0x40000000L;
    internal const long WsPopup = 0x80000000L;
    internal const long WsVisible = 0x10000000L;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpFrameChanged = 0x0020;
    internal const uint SwpShowWindow = 0x0040;

    internal delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindow(string className, string? windowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string? windowName);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    internal static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out IntPtr result);
}
