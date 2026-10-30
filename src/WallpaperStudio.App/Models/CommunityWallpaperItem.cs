namespace WallpaperStudio.App.Models;

public sealed record CommunityWallpaperItem(
    long Id,
    string Title,
    string AuthorName,
    string StoragePath,
    string FileUrl,
    string ThumbnailUrl,
    int PixelWidth,
    int PixelHeight,
    long FileSize,
    string MimeType,
    WallpaperMediaType MediaType,
    DateTime CreatedAt,
    bool IsOwnedByCurrentUser)
{
    public bool IsVideo => MediaType == WallpaperMediaType.Video;
    public string MediaGlyph => IsVideo ? "▶" : string.Empty;
    public string DownloadButtonText => IsVideo ? "下载动态壁纸" : "下载壁纸";
    public string Resolution => IsVideo ? "动态壁纸" : $"{PixelWidth} × {PixelHeight}";
    public string FileSizeText => FileSize switch
    {
        >= 1024L * 1024 => $"{FileSize / 1024d / 1024:0.##} MB",
        >= 1024 => $"{FileSize / 1024d:0.##} KB",
        _ => $"{FileSize} B"
    };

    public string SuggestedFileName => $"{SanitizeFileName(Title)}{MimeType switch
    {
        "video/mp4" => ".mp4",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg"
    }}";

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "wallpaper" : safe;
    }
}
