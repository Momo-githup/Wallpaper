using System.Net.Http;
using Supabase.Postgrest.Exceptions;
using Supabase.Storage;
using Supabase.Storage.Interfaces;
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
        var currentUserId = client.Auth.CurrentUser?.Id;

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
            wallpaper.MediaType == "video" ? WallpaperMediaType.Video : WallpaperMediaType.Image,
            wallpaper.CreatedAt,
            string.Equals(wallpaper.OwnerId, currentUserId, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    public async Task PublishAsync(WallpaperItem wallpaper, CancellationToken cancellationToken = default)
    {
        if (wallpaper.MediaType == WallpaperMediaType.Video &&
            !Path.GetExtension(wallpaper.FilePath).Equals(".mp4", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("社区动态壁纸仅支持 MP4 格式。");

        var client = await GetAuthenticatedClientAsync();
        var userId = client.Auth.CurrentUser?.Id
            ?? throw new InvalidOperationException("登录状态已失效，请重新登录。");
        var files = await _images.PrepareCommunityFilesAsync(wallpaper, cancellationToken);
        var publishedResponse = await client.From<CommunityWallpaperRecord>()
            .Select(record => new object[] { record.Id, record.StoragePath, record.ContentHash! })
            .Where(record => record.Status == "published")
            .Get(cancellationToken);
        if (publishedResponse.Models.Any(record =>
                string.Equals(record.ContentHash, files.ContentHash, StringComparison.OrdinalIgnoreCase)) ||
            await MatchesLegacyWallpaperAsync(client.Storage.From(BucketName), publishedResponse.Models,
                files.LegacyETag, cancellationToken))
            throw new InvalidOperationException("这张壁纸已经发布到社区，不能重复发布。");

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
                MediaType = files.MediaType == WallpaperMediaType.Video ? "video" : "image",
                ContentHash = files.ContentHash,
                Status = "published"
            }, cancellationToken: cancellationToken);
        }
        catch (Exception exception)
        {
            if (uploadedPaths.Count > 0)
            {
                try { await bucket.Remove(uploadedPaths); }
                catch { /* Preserve the original publication error. */ }
            }
            if (exception is PostgrestException postgrestException &&
                (postgrestException.StatusCode == 409 ||
                 postgrestException.Content?.Contains("community_wallpapers_active_content_hash_key", StringComparison.OrdinalIgnoreCase) == true))
            {
                throw new InvalidOperationException("这张壁纸已经发布到社区，不能重复发布。", exception);
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

    public async Task<bool> WithdrawAsync(CommunityWallpaperItem wallpaper,
        CancellationToken cancellationToken = default)
    {
        var client = await GetAuthenticatedClientAsync();
        var userId = client.Auth.CurrentUser?.Id
            ?? throw new InvalidOperationException("登录状态已失效，请重新登录。");
        if (!wallpaper.IsOwnedByCurrentUser)
            throw new InvalidOperationException("只能撤下自己发布的壁纸。");

        var response = await client.From<CommunityWallpaperRecord>()
            .Set(record => record.Status, "removed")
            .Where(record => record.Id == wallpaper.Id)
            .Where(record => record.OwnerId == userId)
            .Update(cancellationToken: cancellationToken);
        if (response.Models.Count == 0)
            throw new InvalidOperationException("壁纸不存在，或当前账户无权撤下。");

        try
        {
            await client.Storage.From(BucketName).Remove([wallpaper.StoragePath, response.Models[0].ThumbnailPath]);
            return true;
        }
        catch
        {
            // The database record is already hidden. Report incomplete cleanup without exposing it again.
            return false;
        }
    }

    private async Task<Supabase.Client> GetAuthenticatedClientAsync()
    {
        await _provider.InitializeAsync();
        var client = _provider.Client;
        if (string.IsNullOrWhiteSpace(client.Auth.CurrentSession?.AccessToken))
            throw new InvalidOperationException("请先登录后使用壁纸社区。");
        return client;
    }

    private static async Task<bool> MatchesLegacyWallpaperAsync(IStorageFileApi<FileObject> bucket,
        IEnumerable<CommunityWallpaperRecord> records, string localETag, CancellationToken cancellationToken)
    {
        foreach (var record in records.Where(record => string.IsNullOrWhiteSpace(record.ContentHash)))
        {
            using var request = new HttpRequestMessage(HttpMethod.Head,
                bucket.GetPublicUrl(record.StoragePath, null!, null));
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode) continue;
            var remoteETag = response.Headers.ETag?.Tag?.Trim('"');
            if (string.Equals(remoteETag, localETag, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
