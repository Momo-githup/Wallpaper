using System.Net.Http;
using Supabase.Storage;
using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

public sealed class SupabaseCommunityService : ICommunityService
{
    private const string BucketName = "wallpapers";
    private static readonly HttpClient Http = new();
    private readonly SupabaseClientProvider _provider;
    private readonly ImageService _images;

    public SupabaseCommunityService(SupabaseClientProvider provider, ImageService images)
    {
        _provider = provider;
        _images = images;
    }

    public async Task<IReadOnlyList<CommunityWallpaperItem>> GetFeedAsync(CancellationToken cancellationToken = default)
    {
        var client = await GetAuthenticatedClientAsync();
        var wallpaperResponse = await client.From<CommunityWallpaperRecord>()
            .Where(wallpaper => wallpaper.Status == "published")
            .Order(wallpaper => wallpaper.CreatedAt, Supabase.Postgrest.Constants.Ordering.Descending)
            .Limit(100)
            .Get(cancellationToken);
        var profileResponse = await client.From<CommunityProfileRecord>()
            .Select(profile => new object[] { profile.Id, profile.DisplayName })
            .Get(cancellationToken);
        var names = profileResponse.Models.ToDictionary(profile => profile.Id, profile => profile.DisplayName);
        var bucket = client.Storage.From(BucketName);

        return wallpaperResponse.Models.Select(wallpaper => new CommunityWallpaperItem(
            wallpaper.Id,
            wallpaper.Title,
            names.GetValueOrDefault(wallpaper.OwnerId, "社区用户"),
            wallpaper.StoragePath,
            bucket.GetPublicUrl(wallpaper.StoragePath, null!, null),
            bucket.GetPublicUrl(wallpaper.ThumbnailPath, null!, null),
            wallpaper.Width,
            wallpaper.Height,
            wallpaper.FileSize,
            wallpaper.MimeType,
            wallpaper.CreatedAt)).ToList();
    }

    public async Task PublishAsync(WallpaperItem wallpaper, CancellationToken cancellationToken = default)
    {
        if (wallpaper.MediaType != WallpaperMediaType.Image)
            throw new InvalidOperationException("社区第一版暂时只支持发布静态图片。");

        var client = await GetAuthenticatedClientAsync();
        var userId = client.Auth.CurrentUser?.Id
            ?? throw new InvalidOperationException("登录状态已失效，请重新登录。");
        var files = await _images.PrepareCommunityFilesAsync(wallpaper.FilePath);
        var key = Guid.NewGuid().ToString("N");
        var storagePath = $"{userId}/{key}{files.Extension}";
        var thumbnailPath = $"{userId}/thumbnails/{key}.jpg";
        var bucket = client.Storage.From(BucketName);
        var uploadedPaths = new List<string>();

        try
        {
            await bucket.Upload(files.FilePath, storagePath,
                new Supabase.Storage.FileOptions { ContentType = files.MimeType, Upsert = false }, null, true, cancellationToken);
            uploadedPaths.Add(storagePath);
            await bucket.Upload(files.ThumbnailPath, thumbnailPath,
                new Supabase.Storage.FileOptions { ContentType = "image/jpeg", Upsert = false }, null, true, cancellationToken);
            uploadedPaths.Add(thumbnailPath);

            var title = Path.GetFileNameWithoutExtension(wallpaper.DisplayName).Trim();
            if (string.IsNullOrWhiteSpace(title)) title = "未命名壁纸";
            if (title.Length > 100) title = title[..100];
            await client.From<CommunityWallpaperRecord>().Insert(new CommunityWallpaperRecord
            {
                OwnerId = userId,
                Title = title,
                StoragePath = storagePath,
                ThumbnailPath = thumbnailPath,
                Width = files.Width,
                Height = files.Height,
                FileSize = files.FileSize,
                MimeType = files.MimeType,
                Status = "published"
            }, cancellationToken: cancellationToken);
        }
        catch
        {
            if (uploadedPaths.Count > 0)
            {
                try { await bucket.Remove(uploadedPaths); }
                catch { /* Preserve the original publication error. */ }
            }
            throw;
        }
    }

    public async Task DownloadAsync(CommunityWallpaperItem wallpaper, string destination,
        CancellationToken cancellationToken = default)
    {
        await GetAuthenticatedClientAsync();
        using var response = await Http.GetAsync(wallpaper.FileUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
    }

    private async Task<Supabase.Client> GetAuthenticatedClientAsync()
    {
        await _provider.InitializeAsync();
        var client = _provider.Client;
        if (string.IsNullOrWhiteSpace(client.Auth.CurrentSession?.AccessToken))
            throw new InvalidOperationException("请先登录后使用壁纸社区。");
        return client;
    }
}
