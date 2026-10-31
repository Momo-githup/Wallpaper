using Velopack.Locators;

namespace WallpaperStudio.App.Services;

internal static class ApplicationExecutable
{
    internal static string CurrentPath => Environment.ProcessPath
        ?? throw new InvalidOperationException("无法确定程序路径。");

    internal static string InstallRoot
    {
        get
        {
            if (VelopackLocator.IsCurrentSet &&
                !string.IsNullOrWhiteSpace(VelopackLocator.Current.RootAppDir))
                return VelopackLocator.Current.RootAppDir;
            return Path.GetDirectoryName(CurrentPath)
                ?? throw new InvalidOperationException("无法确定安装目录。");
        }
    }

    internal static string StableLauncherPath
    {
        get
        {
            if (!VelopackLocator.IsCurrentSet) return CurrentPath;

            var locator = VelopackLocator.Current;
            if (locator.IsPortable || string.IsNullOrWhiteSpace(locator.RootAppDir)) return CurrentPath;

            var launcherName = Path.GetFileName(locator.ThisExeRelativePath);
            if (string.IsNullOrWhiteSpace(launcherName)) launcherName = Path.GetFileName(CurrentPath);
            var launcher = Path.Combine(InstallRoot, launcherName);
            if (File.Exists(launcher)) return launcher;

            var rootLaunchers = Directory.EnumerateFiles(InstallRoot, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(path => !Path.GetFileName(path).Equals("Update.exe", StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToList();
            return rootLaunchers.Count == 1 ? rootLaunchers[0] : CurrentPath;
        }
    }
}
