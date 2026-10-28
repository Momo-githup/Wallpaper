using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

public interface IWallpaperEngine
{
    Task ApplyAsync(string path, WallpaperLayout layout, CancellationToken cancellationToken = default);
}
