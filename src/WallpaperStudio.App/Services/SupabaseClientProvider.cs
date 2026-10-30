namespace WallpaperStudio.App.Services;

public sealed class SupabaseClientProvider
{
    private bool _initialized;

    public SupabaseClientProvider(AppPaths paths)
    {
        Client = new Supabase.Client(
            SupabaseConfiguration.Url,
            SupabaseConfiguration.PublishableKey,
            new Supabase.SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = false,
                SessionHandler = new SecureSessionPersistence(paths)
            });
    }

    public Supabase.Client Client { get; }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        Client.Auth.LoadSession();
        await Client.InitializeAsync();
        _initialized = true;
    }
}
