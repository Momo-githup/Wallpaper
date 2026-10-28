namespace WallpaperStudio.App.Services;

public sealed class AppPaths
{
    public AppPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperStudio");
        Cache = Path.Combine(Root, "Cache");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Cache);
    }

    public string Root { get; }
    public string Cache { get; }
    public string Database => Path.Combine(Root, "library.db");
    public string Settings => Path.Combine(Root, "settings.json");
    public string Log => Path.Combine(Root, "app.log");
}
