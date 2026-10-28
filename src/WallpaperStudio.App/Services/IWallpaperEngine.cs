namespace WallpaperStudio.App.Services;

public interface IWallpaperEngine
{
    Task ApplyAsync(string path, CancellationToken cancellationToken = default);
}
