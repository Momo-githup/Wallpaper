using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using Velopack.Locators;

namespace WallpaperStudio.App.Services;

public sealed class GitHubInstallerUpdateService : IUpdateService
{
    private const string LatestReleaseUrl = "https://github.com/Momo-githup/Wallpaper/releases/latest";
    private const string InstallerAssetName = "WallpaperStudio-Installer-win-x64.exe";
    private const string LatestInstallerUrl =
        "https://github.com/Momo-githup/Wallpaper/releases/latest/download/" + InstallerAssetName;
    private readonly HttpClient _releaseClient = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private readonly HttpClient _downloadClient = new()
    {
        Timeout = TimeSpan.FromMinutes(30)
    };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _pendingInstallerPath;

    public GitHubInstallerUpdateService()
    {
        _releaseClient.DefaultRequestHeaders.UserAgent.ParseAdd("WallpaperStudio-Updater/1.0");
        _downloadClient.DefaultRequestHeaders.UserAgent.ParseAdd("WallpaperStudio-Updater/1.0");
    }

    public bool IsInstalled => VelopackLocator.IsCurrentSet && !VelopackLocator.Current.IsPortable;
    public string? CurrentVersion => IsInstalled
        ? VelopackLocator.Current.CurrentlyInstalledVersion?.ToString()
        : Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3);
    public string? PendingVersion { get; private set; }

    public async Task<UpdateCheckResult> CheckAndDownloadAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInstalled) return new UpdateCheckResult(UpdateCheckStatus.Unsupported);
        if (HasPendingInstaller())
            return new UpdateCheckResult(UpdateCheckStatus.ReadyToInstall, PendingVersion);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (HasPendingInstaller())
                return new UpdateCheckResult(UpdateCheckStatus.ReadyToInstall, PendingVersion);

            using var releaseResponse = await _releaseClient.GetAsync(LatestReleaseUrl,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if ((int)releaseResponse.StatusCode is not (301 or 302 or 307 or 308) ||
                releaseResponse.Headers.Location is null)
                throw new InvalidOperationException("GitHub 没有返回最新版本地址。");

            var tagName = releaseResponse.Headers.Location.OriginalString.TrimEnd('/').Split('/').Last();
            var latestVersion = ParseVersion(Uri.UnescapeDataString(tagName));
            var currentVersion = ParseVersion(CurrentVersion);
            if (latestVersion <= currentVersion)
                return new UpdateCheckResult(UpdateCheckStatus.UpToDate, CurrentVersion);

            var updateDirectory = Path.Combine(Path.GetTempPath(), "WallpaperStudioUpdates", latestVersion.ToString());
            Directory.CreateDirectory(updateDirectory);
            var finalPath = Path.Combine(updateDirectory, InstallerAssetName);
            var partialPath = finalPath + ".download";

            using (var response = await _downloadClient.GetAsync(LatestInstallerUrl,
                       HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(partialPath, FileMode.Create, FileAccess.Write,
                    FileShare.None, 1024 * 128, useAsync: true);
                await input.CopyToAsync(output, cancellationToken);
            }

            File.Move(partialPath, finalPath, overwrite: true);
            _pendingInstallerPath = finalPath;
            PendingVersion = latestVersion.ToString();
            return new UpdateCheckResult(UpdateCheckStatus.ReadyToInstall, PendingVersion);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void ApplyAndRestart()
    {
        if (!HasPendingInstaller())
            throw new InvalidOperationException("没有已经下载完成的更新。");

        var startInfo = new ProcessStartInfo
        {
            FileName = _pendingInstallerPath!,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--update");
        startInfo.ArgumentList.Add("--installto");
        startInfo.ArgumentList.Add(ApplicationExecutable.InstallRoot);
        startInfo.ArgumentList.Add("--waitpid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add("--restart");
        _ = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动更新安装程序。");
    }

    private bool HasPendingInstaller() =>
        !string.IsNullOrWhiteSpace(PendingVersion) &&
        !string.IsNullOrWhiteSpace(_pendingInstallerPath) &&
        File.Exists(_pendingInstallerPath);

    private static Version ParseVersion(string? value)
    {
        var normalized = value?.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        if (!Version.TryParse(normalized, out var version))
            throw new InvalidOperationException($"无法识别版本号：{value ?? "空"}");
        return version;
    }
}
