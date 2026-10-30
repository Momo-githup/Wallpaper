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
    DateTime CreatedAt,
    bool IsOwnedByCurrentUser)
{
    public string Resolution => $"{PixelWidth} × {PixelHeight}";
    public string FileSizeText => FileSize switch
    {
        >= 1024L * 1024 => $"{FileSize / 1024d / 1024:0.##} MB",
        >= 1024 => $"{FileSize / 1024d:0.##} KB",
        _ => $"{FileSize} B"
    };

    public string SuggestedFileName => $"{SanitizeFileName(Title)}{MimeType switch
    {
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
