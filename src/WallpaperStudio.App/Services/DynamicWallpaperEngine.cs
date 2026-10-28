namespace WallpaperStudio.App.Services;

public sealed class DynamicWallpaperEngine
{
    private DynamicWallpaperWindow? _window;

    public async Task ApplyAsync(string path, CancellationToken cancellationToken = default)
    {
        Stop();
        var window = new DynamicWallpaperWindow(path);
        _window = window;
        try
        {
            window.Show();
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
