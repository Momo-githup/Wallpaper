using Velopack;
using Velopack.Sources;

namespace WallpaperStudio.App.Services;

public sealed class VelopackUpdateService : IUpdateService
{
    private const string RepositoryUrl = "https://github.com/Momo-githup/Wallpaper";
    private readonly UpdateManager _manager = new(new GithubSource(RepositoryUrl, null, false));
    private readonly SemaphoreSlim _gate = new(1, 1);

    public bool IsInstalled => _manager.IsInstalled;
    public string? CurrentVersion => _manager.CurrentVersion?.ToString();
    public string? PendingVersion => _manager.UpdatePendingRestart?.Version?.ToString();

    public async Task<UpdateCheckResult> CheckAndDownloadAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInstalled) return new UpdateCheckResult(UpdateCheckStatus.Unsupported);
        if (_manager.UpdatePendingRestart is { } pending)
            return new UpdateCheckResult(UpdateCheckStatus.ReadyToInstall, pending.Version?.ToString());

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_manager.UpdatePendingRestart is { } queued)
                return new UpdateCheckResult(UpdateCheckStatus.ReadyToInstall, queued.Version?.ToString());

            var update = await _manager.CheckForUpdatesAsync();
            if (update is null) return new UpdateCheckResult(UpdateCheckStatus.UpToDate, CurrentVersion);

            await _manager.DownloadUpdatesAsync(update, cancelToken: cancellationToken);
            var downloaded = _manager.UpdatePendingRestart ?? update.TargetFullRelease;
            return new UpdateCheckResult(UpdateCheckStatus.ReadyToInstall, downloaded.Version?.ToString());
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ApplyAndRestart()
    {
        var pending = _manager.UpdatePendingRestart
            ?? throw new InvalidOperationException("没有已经下载完成的更新。");
        _manager.WaitExitThenApplyUpdates(pending, silent: false, restart: true, restartArgs: []);
    }
}
