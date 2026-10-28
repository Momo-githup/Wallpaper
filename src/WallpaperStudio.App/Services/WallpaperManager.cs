using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

public sealed class WallpaperManager
{
    private readonly IWallpaperEngine _staticEngine;
    private readonly DynamicWallpaperEngine _dynamicEngine;

    public WallpaperManager(IWallpaperEngine staticEngine, DynamicWallpaperEngine dynamicEngine)
    {
        _staticEngine = staticEngine;
        _dynamicEngine = dynamicEngine;
    }

    public async Task ApplyAsync(WallpaperItem item, CancellationToken cancellationToken = default)
    {
        if (item.MediaType == WallpaperMediaType.Video)
        {
            await _dynamicEngine.ApplyAsync(item.FilePath, cancellationToken);
            return;
        }

        _dynamicEngine.Stop();
        await _staticEngine.ApplyAsync(item.FilePath, cancellationToken);
    }

    public void StopDynamic() => _dynamicEngine.Stop();
}
