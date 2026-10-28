using System.Runtime.InteropServices;

namespace WallpaperStudio.App.Services;

public sealed class StaticWallpaperEngine : IWallpaperEngine
{
    private readonly ImageService _images;

    public StaticWallpaperEngine(ImageService images) => _images = images;

    public async Task ApplyAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("仅支持 Windows。 ");
        if (!File.Exists(path)) throw new FileNotFoundException("壁纸文件不存在。", path);
        cancellationToken.ThrowIfCancellationRequested();
        var displayPath = await _images.PrepareDisplayFileAsync(path);

        IDesktopWallpaper? desktopWallpaper = null;
        try
        {
            desktopWallpaper = (IDesktopWallpaper)new DesktopWallpaperClass();
            desktopWallpaper.SetPosition(DesktopWallpaperPosition.Fill);
            desktopWallpaper.SetWallpaper(null, displayPath);
        }
        catch (COMException exception)
        {
            throw new InvalidOperationException("Windows 拒绝设置壁纸，请检查文件格式或系统策略。", exception);
        }
        finally
        {
            if (desktopWallpaper is not null && Marshal.IsComObject(desktopWallpaper))
                Marshal.FinalReleaseComObject(desktopWallpaper);
        }
    }

}

internal enum DesktopWallpaperPosition
{
    Center,
    Tile,
    Stretch,
    Fit,
    Fill,
    Span
}

[ComImport]
[Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")]
internal class DesktopWallpaperClass;

[ComImport]
[Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IDesktopWallpaper
{
    void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
    [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorId);
    [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint monitorIndex);
    uint GetMonitorDevicePathCount();
    void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorId, out NativeRect displayRect);
    void SetBackgroundColor(uint color);
    uint GetBackgroundColor();
    void SetPosition(DesktopWallpaperPosition position);
    DesktopWallpaperPosition GetPosition();
    void SetSlideshow(IntPtr items);
    IntPtr GetSlideshow();
    void SetSlideshowOptions(int options, uint slideshowTick);
    void GetSlideshowOptions(out int options, out uint slideshowTick);
    void AdvanceSlideshow([MarshalAs(UnmanagedType.LPWStr)] string? monitorId, int direction);
    int GetStatus();
    void Enable([MarshalAs(UnmanagedType.Bool)] bool enable);
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}
