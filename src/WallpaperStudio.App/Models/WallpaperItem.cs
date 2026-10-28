namespace WallpaperStudio.App.Models;

public sealed record WallpaperItem(
    long Id,
    string FilePath,
    string DisplayName,
    int PixelWidth,
    int PixelHeight,
    long FileSize,
    DateTime ImportedAt,
    bool IsFavorite,
    WallpaperMediaType MediaType = WallpaperMediaType.Image)
{
    public string Resolution => MediaType == WallpaperMediaType.Video
        ? "动态壁纸"
        : PixelWidth > 0 ? $"{PixelWidth} × {PixelHeight}" : "未知";
    public string FileSizeText => FileSize switch
    {
        >= 1024L * 1024 * 1024 => $"{FileSize / 1024d / 1024 / 1024:0.##} GB",
        >= 1024L * 1024 => $"{FileSize / 1024d / 1024:0.##} MB",
        >= 1024 => $"{FileSize / 1024d:0.##} KB",
        _ => $"{FileSize} B"
    };
}
