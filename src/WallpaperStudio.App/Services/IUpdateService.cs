namespace WallpaperStudio.App.Services;

public enum UpdateCheckStatus
{
    Unsupported,
    UpToDate,
    ReadyToInstall
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, string? Version = null);

public interface IUpdateService
{
    bool IsInstalled { get; }
    string? CurrentVersion { get; }
    string? PendingVersion { get; }
    Task<UpdateCheckResult> CheckAndDownloadAsync(CancellationToken cancellationToken = default);
    void ApplyAndRestart();
}
