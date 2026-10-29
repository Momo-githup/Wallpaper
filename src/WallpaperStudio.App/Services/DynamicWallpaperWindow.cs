using LibVLCSharp.Shared;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WallpaperStudio.App.Services;

internal sealed class DynamicWallpaperWindow : IDisposable
{
    private readonly string _path;
    private readonly TaskCompletionSource _opened = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private HwndSource? _source;
    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private Media? _media;
    private IntPtr _desktopHost;
    private IntPtr _desktopIconView;
    private bool _disposed;

    public DynamicWallpaperWindow(string path)
    {
        _path = path;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void Start()
    {
        if (_source is not null) return;
        var desktop = DesktopHost.Find();
        _desktopHost = desktop.Host;
        _desktopIconView = desktop.IconView;
        if (_desktopHost == IntPtr.Zero || !NativeMethods.GetClientRect(_desktopHost, out var rect))
            throw new InvalidOperationException("无法连接 Windows 桌面窗口。");

        var parameters = new HwndSourceParameters("WallpaperStudio.DynamicWallpaper",
            rect.Right - rect.Left, rect.Bottom - rect.Top)
        {
            ParentWindow = _desktopHost,
            PositionX = 0,
            PositionY = 0,
            WindowStyle = (int)(NativeMethods.WsChild | NativeMethods.WsVisible |
                NativeMethods.WsClipChildren | NativeMethods.WsClipSiblings),
            ExtendedWindowStyle = NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow
        };
        _source = new HwndSource(parameters);
        ResizeToDesktop();

        Core.Initialize();
        // Explorer's desktop host does not reliably composite Direct3D video surfaces.
        // The WinGDI output writes every frame into the child HWND and remains visible
        // behind desktop icons on Windows 10/11.
        _libVlc = new LibVLC("--no-audio", "--no-video-title-show", "--quiet",
            "--avcodec-hw=none", "--vout=wingdi", "--input-repeat=-1");
        _player = new MediaPlayer(_libVlc)
        {
            Hwnd = _source.Handle,
            EnableHardwareDecoding = false
        };
        _player.Playing += OnPlaying;
        _player.EndReached += OnEndReached;
        _player.EncounteredError += OnEncounteredError;
        _media = new Media(_libVlc, new Uri(_path));
        _media.AddOption(":input-repeat=-1");
        _media.AddOption(":no-audio");
        if (!_player.Play(_media))
            throw new InvalidOperationException("无法启动视频播放器。");
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
        Dispose();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        if (_player is not null)
        {
            _player.Playing -= OnPlaying;
            _player.EndReached -= OnEndReached;
            _player.EncounteredError -= OnEncounteredError;
            _player.Stop();
            _player.Dispose();
            _player = null;
        }
        _media?.Dispose();
        _media = null;
        _libVlc?.Dispose();
        _libVlc = null;
        _source?.Dispose();
        _source = null;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        _source?.Dispatcher.Invoke(ResizeToDesktop);
    }

    private void OnPlaying(object? sender, EventArgs e) => _opened.TrySetResult();

    private void OnEndReached(object? sender, EventArgs e)
    {
        // input-repeat normally loops inside VLC without closing the video output.
        // Keep this as a fallback, but never call Stop here: Stop clears the HWND
        // and briefly exposes the static wallpaper underneath it.
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (_disposed || _player is null || _media is null) return;
            _player.Play(_media);
        });
    }

    private void OnEncounteredError(object? sender, EventArgs e) =>
        _opened.TrySetException(new InvalidOperationException("LibVLC 无法播放这个视频。"));

    private void ResizeToDesktop()
    {
        if (_source is null || _desktopHost == IntPtr.Zero || !NativeMethods.GetClientRect(_desktopHost, out var rect)) return;
        var insertAfter = _desktopIconView != IntPtr.Zero ? _desktopIconView : new IntPtr(1);
        NativeMethods.SetWindowPos(_source.Handle, insertAfter, 0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow | NativeMethods.SwpFrameChanged);
    }
}

internal static class DesktopHost
{
    public static DesktopTarget Find()
    {
        var progman = NativeMethods.FindWindow("Progman", null);
        if (progman == IntPtr.Zero) return default;

        NativeMethods.SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, 0, 1000, out _);
        var iconHost = IntPtr.Zero;
        var iconView = IntPtr.Zero;
        NativeMethods.EnumWindows((top, _) =>
        {
            var shellView = NativeMethods.FindWindowEx(top, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView == IntPtr.Zero) return true;
            iconHost = top;
            iconView = shellView;
            return false;
        }, IntPtr.Zero);
        if (iconHost == IntPtr.Zero)
        {
            iconHost = progman;
            iconView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        }
        return new DesktopTarget(iconHost, iconView);
    }
}

internal readonly record struct DesktopTarget(IntPtr Host, IntPtr IconView);

internal static class NativeMethods
{
    internal const long WsChild = 0x40000000L;
    internal const long WsVisible = 0x10000000L;
    internal const long WsClipChildren = 0x02000000L;
    internal const long WsClipSiblings = 0x04000000L;
    internal const int WsExToolWindow = 0x00000080;
    internal const int WsExNoActivate = 0x08000000;
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
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(IntPtr hwnd, out NativeRect rect);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern IntPtr SendMessageTimeout(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out IntPtr result);
}
