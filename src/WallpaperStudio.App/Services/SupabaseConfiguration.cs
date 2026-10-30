namespace WallpaperStudio.App.Services;

internal static class SupabaseConfiguration
{
    private const string DefaultUrl = "https://oksoqcdehmkyiedhwfev.supabase.co";
    private const string DefaultPublishableKey = "sb_publishable_hWSChQAKtxpOITUAHxQ3Sw_K_d6MwyB";

    public static string Url => Environment.GetEnvironmentVariable("WALLPAPERSTUDIO_SUPABASE_URL") ?? DefaultUrl;
    public static string PublishableKey => Environment.GetEnvironmentVariable("WALLPAPERSTUDIO_SUPABASE_KEY") ?? DefaultPublishableKey;
}
