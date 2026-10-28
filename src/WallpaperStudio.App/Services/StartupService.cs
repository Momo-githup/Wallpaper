using Microsoft.Win32;

namespace WallpaperStudio.App.Services;

public sealed class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WallpaperStudio";

    public void EnsureDisabled()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        key.DeleteValue(ValueName, false);
    }
}
