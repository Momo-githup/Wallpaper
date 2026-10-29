namespace WallpaperStudio.App.Services;

public sealed class DynamicWallpaperEngine
{
    private readonly string _cacheDirectory;
    private DynamicWallpaperWindow? _window;

    public DynamicWallpaperEngine(string cacheDirectory) => _cacheDirectory = cacheDirectory;

    public async Task ApplyAsync(string path, CancellationToken cancellationToken = default)
    {
        Stop();
        DynamicWallpaperWindow? bootstrapWindow = null;
        try
        {
            var window = new DynamicWallpaperWindow(path, _cacheDirectory);
            if (!window.TryApplyCachedFallback())
            {
                bootstrapWindow = window;
                bootstrapWindow.Start();
                await bootstrapWindow.WaitUntilOpenedAsync(cancellationToken);
                await bootstrapWindow.PrepareDesktopFallbackAsync(cancellationToken);
                bootstrapWindow.Dispose();
                bootstrapWindow = null;
                window = new DynamicWallpaperWindow(path, _cacheDirectory);
            }

            // IDesktopWallpaper can return before Explorer has finished
            // rebuilding its desktop host. Create the real video surface only
            // after that refresh settles, otherwise it can be placed in the
            // obsolete host and remain hidden behind the fallback image.
            await Task.Delay(500, cancellationToken);
            _window = window;
            window.Start();
            await window.WaitUntilOpenedAsync(cancellationToken);
        }
        catch
        {
            bootstrapWindow?.Dispose();
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        var window = _window;
        _window = null;
        window?.StopPlayback();
    }
}
