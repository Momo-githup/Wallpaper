using System.Text.Json;
using System.Text.Json.Serialization;
using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly AppPaths _paths;

    public SettingsService(AppPaths paths) => _paths = paths;

    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(_paths.Settings)) return new AppSettings();
            await using var stream = File.OpenRead(_paths.Settings);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions)
                ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings)
    {
        await using var stream = File.Create(_paths.Settings);
        await JsonSerializer.SerializeAsync(stream, settings, JsonOptions);
    }
}
