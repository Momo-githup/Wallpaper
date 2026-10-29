using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

public sealed class WallpaperManager
{
    private readonly IWallpaperEngine _staticEngine;
    private readonly DynamicWallpaperHostClient _dynamicHost;

    public WallpaperManager(IWallpaperEngine staticEngine, DynamicWallpaperHostClient dynamicHost)
    {
        _staticEngine = staticEngine;
        _dynamicHost = dynamicHost;
    }

    public async Task ApplyAsync(WallpaperItem item, CancellationToken cancellationToken = default)
    {
        if (item.MediaType == WallpaperMediaType.Video)
        {
            await _dynamicHost.ApplyAsync(item.FilePath, cancellationToken);
            return;
        }

        await _dynamicHost.StopAsync(cancellationToken);
        await _staticEngine.ApplyAsync(item.FilePath, cancellationToken);
    }
}
