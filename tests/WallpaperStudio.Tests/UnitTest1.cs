using WallpaperStudio.App.Models;
using WallpaperStudio.App.Services;
using Supabase.Gotrue;

namespace WallpaperStudio.Tests;

public class CoreTests
{
    [Fact]
    public void WallpaperItem_FormatsMetadata()
    {
        var item = new WallpaperItem(1, @"C:\photo.jpg", "photo", 3840, 2160, 5 * 1024 * 1024, DateTime.UtcNow, false);
        Assert.Equal("3840 × 2160", item.Resolution);
        Assert.Equal("5 MB", item.FileSizeText);
    }

    [Fact]
    public void WallpaperItem_LabelsVideoAsDynamicWallpaper()
    {
        var item = new WallpaperItem(2, @"C:\video.mp4", "video", 0, 0, 1024, DateTime.UtcNow, false, WallpaperMediaType.Video);
        Assert.Equal("动态壁纸", item.Resolution);
    }

    [Theory]
    [InlineData("photo.JPG")]
    [InlineData("photo.webp")]
    [InlineData("photo.png")]
    public void ImageService_RecognizesSupportedExtensions(string file)
    {
        var root = Path.Combine(Path.GetTempPath(), "WallpaperStudioTests", Guid.NewGuid().ToString("N"));
        try { Assert.True(new ImageService(new AppPaths(root)).IsSupported(file)); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("wallpaper.mp4")]
    [InlineData("wallpaper.WMV")]
    [InlineData("wallpaper.mov")]
    public void MediaFileService_RecognizesVideoExtensions(string file)
    {
        var root = Path.Combine(Path.GetTempPath(), "WallpaperStudioTests", Guid.NewGuid().ToString("N"));
        try
        {
            var images = new ImageService(new AppPaths(root));
            var media = new MediaFileService(images);
            Assert.True(media.IsSupported(file));
            Assert.Equal(WallpaperMediaType.Video, media.GetMediaType(file));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task LibraryDatabase_StoresVideoMediaType()
    {
        var root = Path.Combine(Path.GetTempPath(), "WallpaperStudioTests", Guid.NewGuid().ToString("N"));
        try
        {
            var database = new LibraryDatabase(new AppPaths(root));
            await database.InitializeAsync();
            await database.AddAsync([(Path.Combine(root, "wallpaper.mp4"), 0, 0, 2048, WallpaperMediaType.Video)]);
            var item = Assert.Single(await database.GetAllAsync());
            Assert.Equal(WallpaperMediaType.Video, item.MediaType);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SettingsService_RoundTripsSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "WallpaperStudioTests", Guid.NewGuid().ToString("N"));
        try
        {
            var service = new SettingsService(new AppPaths(root));
            await service.SaveAsync(new AppSettings
            {
                CurrentWallpaperPath = "x.mp4",
                CurrentWallpaperMediaType = WallpaperMediaType.Video
            });
            var loaded = await service.LoadAsync();
            Assert.Equal("x.mp4", loaded.CurrentWallpaperPath);
            Assert.Equal(WallpaperMediaType.Video, loaded.CurrentWallpaperMediaType);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void SecureSessionPersistence_EncryptsRoundTripsAndDestroysSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "WallpaperStudioTests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            var persistence = new SecureSessionPersistence(paths);
            persistence.SaveSession(new Session
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                TokenType = "bearer",
                ExpiresIn = 3600,
                CreatedAt = DateTime.UtcNow
            });

            var rawBytes = File.ReadAllBytes(paths.AuthSession);
            Assert.DoesNotContain("access-token", System.Text.Encoding.UTF8.GetString(rawBytes));
            Assert.Equal("access-token", persistence.LoadSession()?.AccessToken);
            Assert.Equal("refresh-token", persistence.LoadSession()?.RefreshToken);

            persistence.DestroySession();
            Assert.False(File.Exists(paths.AuthSession));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ImageService_PreparesMp4ForCommunity()
    {
        var root = Path.Combine(Path.GetTempPath(), "WallpaperStudioTests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root);
            var videoPath = Path.Combine(root, "wallpaper.mp4");
            await File.WriteAllBytesAsync(videoPath,
            [
                0, 0, 0, 24, (byte)'f', (byte)'t', (byte)'y', (byte)'p',
                (byte)'i', (byte)'s', (byte)'o', (byte)'m', 0, 0, 0, 0,
                (byte)'i', (byte)'s', (byte)'o', (byte)'m', (byte)'m', (byte)'p', (byte)'4', (byte)'2'
            ]);
            var wallpaper = new WallpaperItem(1, videoPath, "wallpaper.mp4", 0, 0,
                new FileInfo(videoPath).Length, DateTime.UtcNow, false, WallpaperMediaType.Video);

            var prepared = await new ImageService(paths).PrepareCommunityFilesAsync(wallpaper);

            Assert.Equal("video/mp4", prepared.MimeType);
            Assert.Equal(WallpaperMediaType.Video, prepared.MediaType);
            Assert.Equal(64, prepared.ContentHash.Length);
            Assert.True(File.Exists(prepared.ThumbnailPath));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
