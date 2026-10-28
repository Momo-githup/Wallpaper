using WallpaperStudio.App.Models;
using WallpaperStudio.App.Services;

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
            await service.SaveAsync(new AppSettings { CurrentWallpaperPath = "x.jpg" });
            var loaded = await service.LoadAsync();
            Assert.Equal("x.jpg", loaded.CurrentWallpaperPath);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
