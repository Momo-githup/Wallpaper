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
