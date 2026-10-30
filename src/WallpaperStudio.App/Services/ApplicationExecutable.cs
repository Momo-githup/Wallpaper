using Velopack.Locators;

namespace WallpaperStudio.App.Services;

internal static class ApplicationExecutable
{
    internal static string CurrentPath => Environment.ProcessPath
        ?? throw new InvalidOperationException("无法确定程序路径。");

    internal static string StableLauncherPath
    {
        get
        {
            if (!VelopackLocator.IsCurrentSet) return CurrentPath;

            var locator = VelopackLocator.Current;
            if (locator.IsPortable || string.IsNullOrWhiteSpace(locator.RootAppDir)) return CurrentPath;

            var launcherName = Path.GetFileName(locator.ThisExeRelativePath);
            if (string.IsNullOrWhiteSpace(launcherName)) launcherName = Path.GetFileName(CurrentPath);
            var launcher = Path.Combine(locator.RootAppDir, launcherName);
            if (File.Exists(launcher)) return launcher;

            var rootLaunchers = Directory.EnumerateFiles(locator.RootAppDir, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(path => !Path.GetFileName(path).Equals("Update.exe", StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();
            return rootLaunchers.Count == 1 ? rootLaunchers[0] : CurrentPath;
        }
    }
}
