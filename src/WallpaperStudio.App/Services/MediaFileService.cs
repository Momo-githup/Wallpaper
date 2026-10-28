using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

public sealed class MediaFileService
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".wmv", ".avi", ".mov", ".m4v"
    };

    private readonly ImageService _images;

    public MediaFileService(ImageService images) => _images = images;

    public bool IsSupported(string path) => _images.IsSupported(path) || IsVideo(path);

    public bool IsVideo(string path) => VideoExtensions.Contains(Path.GetExtension(path));

    public WallpaperMediaType GetMediaType(string path) => IsVideo(path)
        ? WallpaperMediaType.Video
        : WallpaperMediaType.Image;

    public async Task<(int Width, int Height, long Size)?> InspectAsync(string path)
    {
        if (!File.Exists(path)) return null;
        if (IsVideo(path)) return (0, 0, new FileInfo(path).Length);
        return await _images.InspectAsync(path);
    }
}
