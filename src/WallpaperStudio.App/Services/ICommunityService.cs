using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

public interface ICommunityService
{
    Task<IReadOnlyList<CommunityWallpaperItem>> GetFeedAsync(CancellationToken cancellationToken = default);
    Task PublishAsync(WallpaperItem wallpaper, CancellationToken cancellationToken = default);
    Task DownloadAsync(CommunityWallpaperItem wallpaper, string destination, CancellationToken cancellationToken = default);
    Task WithdrawAsync(CommunityWallpaperItem wallpaper, CancellationToken cancellationToken = default);
}
