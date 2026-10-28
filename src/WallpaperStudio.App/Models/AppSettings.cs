namespace WallpaperStudio.App.Models;

public sealed class AppSettings
{
    public WallpaperLayout Layout { get; set; } = WallpaperLayout.Fill;
    public bool StartWithWindows { get; set; }
    public string? CurrentWallpaperPath { get; set; }
}
