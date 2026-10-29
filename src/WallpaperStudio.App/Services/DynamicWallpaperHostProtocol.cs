using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using System.Windows.Threading;
using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

internal static class DynamicWallpaperHostProtocol
{
    internal const string PipeName = "WallpaperStudio.DynamicWallpaperHost.v1";
    internal const string MutexName = "Local\\WallpaperStudio.DynamicWallpaperHost.v1";
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

internal sealed record DynamicHostRequest(string Command, string? Path = null);
internal sealed record DynamicHostResponse(bool Success, string? Error = null);

public sealed class DynamicWallpaperHostClient
{
    public async Task ApplyAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("视频文件不存在。", path);
        await EnsureHostAsync(cancellationToken);
        // A never-before-used video may require a bootstrap decode, snapshot,
        // Explorer refresh and then the real playback startup. Keep the client
        // connected for the full bounded operation so it does not abandon a
        // healthy host halfway through that first-time preparation.
        var response = await SendAsync(new DynamicHostRequest("apply", path), TimeSpan.FromSeconds(60), cancellationToken);
        if (!response.Success) throw new InvalidOperationException(response.Error ?? "动态壁纸引擎启动失败。");
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await SendAsync(new DynamicHostRequest("stop"), TimeSpan.FromSeconds(5), cancellationToken);
            if (!response.Success) throw new InvalidOperationException(response.Error ?? "无法停止动态壁纸引擎。");
        }
        catch (TimeoutException)
        {
            // No host is running, so there is nothing to stop.
        }
    }

    private static async Task EnsureHostAsync(CancellationToken cancellationToken)
    {
        if (await CanConnectAsync(cancellationToken)) return;
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("无法确定程序路径。");
        Process.Start(new ProcessStartInfo
        {
            FileName = executable,
            Arguments = "--wallpaper-host",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(100, cancellationToken);
            if (await CanConnectAsync(cancellationToken)) return;
        }
        throw new TimeoutException("动态壁纸后台引擎启动超时。");
    }

    private static async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await SendAsync(new DynamicHostRequest("ping"), TimeSpan.FromMilliseconds(750), cancellationToken);
            return response.Success;
        }
        catch (TimeoutException) { return false; }
        catch (IOException) { return false; }
    }

    private static async Task<DynamicHostResponse> SendAsync(DynamicHostRequest request, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await using var pipe = new NamedPipeClientStream(".", DynamicWallpaperHostProtocol.PipeName,
                PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            await pipe.ConnectAsync(timeoutSource.Token);
            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, leaveOpen: true);
            await writer.WriteLineAsync(JsonSerializer.Serialize(request, DynamicWallpaperHostProtocol.JsonOptions));
            var line = await reader.ReadLineAsync(timeoutSource.Token);
            return line is null
                ? new DynamicHostResponse(false, "动态壁纸引擎未返回结果。")
                : JsonSerializer.Deserialize<DynamicHostResponse>(line, DynamicWallpaperHostProtocol.JsonOptions)
                  ?? new DynamicHostResponse(false, "动态壁纸引擎返回了无效结果。");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("无法连接动态壁纸后台引擎。");
        }
    }
}

internal sealed class DynamicWallpaperHostServer : IDisposable
{
    private readonly AppPaths _paths;
    private readonly Dispatcher _dispatcher;
    private readonly DynamicWallpaperEngine _engine;
    private readonly CancellationTokenSource _restoreCancellation = new();
    private Task? _restoreTask;
    private Mutex? _mutex;
    private bool _stopping;

    internal DynamicWallpaperHostServer(AppPaths paths, Dispatcher dispatcher)
    {
        _paths = paths;
        _dispatcher = dispatcher;
        _engine = new DynamicWallpaperEngine(paths.Cache);
    }

    internal bool TryAcquireSingleInstance()
    {
        _mutex = new Mutex(true, DynamicWallpaperHostProtocol.MutexName, out var createdNew);
        return createdNew;
    }

    internal async Task RunAsync()
    {
        // Make the pipe available immediately. Restoring a video can take
        // several seconds, and doing it before listening made the UI believe
        // the host had failed to start and launch redundant retry processes.
        _restoreTask = RestoreLastDynamicWallpaperAsync(_restoreCancellation.Token);
        while (!_stopping)
        {
            try
            {
                await ProcessNextConnectionAsync();
            }
            catch (IOException exception)
            {
                // A client may time out and close its end while an apply request
                // is still completing. That connection is gone, but the host
                // and the successfully started wallpaper must remain alive.
                await AppendLogAsync($"动态壁纸客户端已断开，宿主继续运行: {exception.Message}");
            }
        }
        _ = _dispatcher.BeginInvoke(System.Windows.Application.Current.Shutdown);
    }

    private async Task ProcessNextConnectionAsync()
    {
        await using var pipe = new NamedPipeServerStream(DynamicWallpaperHostProtocol.PipeName,
            PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.WaitForConnectionAsync();
        using var reader = new StreamReader(pipe, leaveOpen: true);
        using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        var line = await reader.ReadLineAsync();
        var response = await HandleAsync(line);
        await writer.WriteLineAsync(JsonSerializer.Serialize(response, DynamicWallpaperHostProtocol.JsonOptions));
    }

    private async Task<DynamicHostResponse> HandleAsync(string? line)
    {
        DynamicHostRequest? request = null;
        try
        {
            request = line is null ? null : JsonSerializer.Deserialize<DynamicHostRequest>(line,
                DynamicWallpaperHostProtocol.JsonOptions);
            switch (request?.Command)
            {
                case "ping": return new DynamicHostResponse(true);
                case "apply" when !string.IsNullOrWhiteSpace(request.Path):
                    await CancelPendingRestoreAsync();
                    await _engine.ApplyAsync(request.Path);
                    return new DynamicHostResponse(true);
                case "stop":
                    await CancelPendingRestoreAsync();
                    _engine.Stop();
                    _stopping = true;
                    return new DynamicHostResponse(true);
                default: return new DynamicHostResponse(false, "未知的动态壁纸命令。");
            }
        }
        catch (Exception exception)
        {
            await AppendLogAsync($"动态壁纸命令 {request?.Command ?? "unknown"} 失败: {exception}");
            return new DynamicHostResponse(false, exception.Message);
        }
    }

    private Task AppendLogAsync(string message) =>
        File.AppendAllTextAsync(_paths.Log, $"{DateTime.Now:O} {message}\n");

    private async Task CancelPendingRestoreAsync()
    {
        _restoreCancellation.Cancel();
        if (_restoreTask is null) return;
        await _restoreTask;
        _restoreTask = null;
    }

    private async Task RestoreLastDynamicWallpaperAsync(CancellationToken cancellationToken)
    {
        try
        {
            // A freshly launched UI sends its explicit apply request almost
            // immediately. Give that request priority and only auto-restore
            // when the host was started on its own at Windows sign-in.
            await Task.Delay(1000, cancellationToken);
            var settings = await new SettingsService(_paths).LoadAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var path = settings.CurrentWallpaperPath;
            var isVideo = settings.CurrentWallpaperMediaType == WallpaperMediaType.Video ||
                (settings.CurrentWallpaperMediaType is null && string.Equals(Path.GetExtension(path), ".mp4",
                    StringComparison.OrdinalIgnoreCase));
            if (!isVideo || string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
            await _engine.ApplyAsync(path, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // An explicit apply/stop request superseded startup restoration.
        }
        catch (Exception exception)
        {
            await File.AppendAllTextAsync(_paths.Log, $"{DateTime.Now:O} 恢复动态壁纸失败: {exception}\n");
        }
    }

    public void Dispose()
    {
        _restoreCancellation.Cancel();
        _engine.Stop();
        _restoreCancellation.Dispose();
        _mutex?.Dispose();
    }
}
