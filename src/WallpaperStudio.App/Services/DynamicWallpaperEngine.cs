namespace WallpaperStudio.App.Services;

public sealed class DynamicWallpaperEngine
{
    private readonly string _cacheDirectory;
    private DynamicWallpaperWindow? _window;

    public DynamicWallpaperEngine(string cacheDirectory) => _cacheDirectory = cacheDirectory;

    public async Task ApplyAsync(string path, CancellationToken cancellationToken = default)
    {
        Stop();
        var window = new DynamicWallpaperWindow(path, _cacheDirectory);
        _window = window;
        try
        {
            window.Start();
            await window.WaitUntilOpenedAsync(cancellationToken);
        }
        catch
        {
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
