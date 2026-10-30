using System.Security.Cryptography;
using System.Text.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace WallpaperStudio.App.Services;

public sealed class SecureSessionPersistence : IGotrueSessionPersistence<Session>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _path;

    public SecureSessionPersistence(AppPaths paths) => _path = paths.AuthSession;

    public void SaveSession(Session session)
    {
        var serialized = JsonSerializer.SerializeToUtf8Bytes(session, JsonOptions);
        var encrypted = ProtectedData.Protect(serialized, null, DataProtectionScope.CurrentUser);
        var temporaryPath = _path + ".tmp";
        File.WriteAllBytes(temporaryPath, encrypted);
        File.Move(temporaryPath, _path, true);
    }

    public Session? LoadSession()
    {
        if (!File.Exists(_path)) return null;

        try
        {
            var encrypted = File.ReadAllBytes(_path);
            var serialized = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<Session>(serialized, JsonOptions);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or IOException)
        {
            TryDeleteSessionFile();
            return null;
        }
    }

    public void DestroySession() => TryDeleteSessionFile();

    public Task SaveSessionAsync(Session session, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SaveSession(session);
        return Task.CompletedTask;
    }

    public Task<Session?> LoadSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(LoadSession());
    }

    public Task DestroySessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DestroySession();
        return Task.CompletedTask;
    }

    private void TryDeleteSessionFile()
    {
        try
        {
            if (File.Exists(_path)) File.Delete(_path);
            var temporaryPath = _path + ".tmp";
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
