using System.Configuration;
using System.Data;
using System.Windows;

using System.Drawing;
using WallpaperStudio.App.Services;
using WallpaperStudio.App.ViewModels;
using WinForms = System.Windows.Forms;

namespace WallpaperStudio.App;

public partial class App : System.Windows.Application
{
    private WinForms.NotifyIcon? _trayIcon;
    private Icon? _appIcon;
    private MainWindow? _mainWindow;
    private AppPaths? _paths;
    private DynamicWallpaperHostServer? _hostServer;
    private DynamicWallpaperHostClient? _hostClient;
    private IUpdateService? _updateService;
    private CancellationTokenSource? _lifetime;
    private WinForms.ToolStripMenuItem? _updateMenuItem;
    private string? _lastPromptedUpdate;
    private bool _isExiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _paths = new AppPaths();
        var isWallpaperHost = e.Args.Contains("--wallpaper-host", StringComparer.OrdinalIgnoreCase);
        DispatcherUnhandledException += (_, args) =>
        {
            File.AppendAllText(_paths.Log, $"{DateTime.Now:O} {args.Exception}\n");
            if (!isWallpaperHost)
                MessageBox.Show("发生了未处理的错误，详情已写入日志。", "Wallpaper Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            if (isWallpaperHost) Shutdown();
        };
        if (isWallpaperHost)
        {
            _hostServer = new DynamicWallpaperHostServer(_paths, Dispatcher);
            if (!_hostServer.TryAcquireSingleInstance())
            {
                Shutdown();
                return;
            }
            _ = RunWallpaperHostAsync();
            return;
        }

        var images = new ImageService(_paths);
        var startup = new StartupService();
        var savedSettings = await new SettingsService(_paths).LoadAsync();
        var savedPath = savedSettings.CurrentWallpaperPath;
        var hasSavedDynamicWallpaper = savedSettings.CurrentWallpaperMediaType == Models.WallpaperMediaType.Video ||
            (savedSettings.CurrentWallpaperMediaType is null &&
             string.Equals(Path.GetExtension(savedPath), ".mp4", StringComparison.OrdinalIgnoreCase));
        if (hasSavedDynamicWallpaper && !string.IsNullOrWhiteSpace(savedPath) && File.Exists(savedPath))
            startup.EnableDynamicHost();
        var mediaFiles = new MediaFileService(images);
        _hostClient = new DynamicWallpaperHostClient();
        var wallpaperManager = new WallpaperManager(new StaticWallpaperEngine(images), _hostClient);
        var supabase = new SupabaseClientProvider(_paths);
        var authService = new SupabaseAuthService(supabase);
        var communityService = new SupabaseCommunityService(supabase, images);
        _updateService = new VelopackUpdateService();
        _lifetime = new CancellationTokenSource();
        var viewModel = new MainViewModel(new LibraryDatabase(_paths), images, mediaFiles, wallpaperManager,
            new SettingsService(_paths), startup, authService, communityService);
        _mainWindow = new MainWindow(viewModel);
        _mainWindow.Closing += (_, args) =>
        {
            if (_isExiting) return;
            args.Cancel = true;
            _mainWindow.Hide();
            _trayIcon?.ShowBalloonTip(1500, "Wallpaper Studio", "应用仍在托盘中运行。", WinForms.ToolTipIcon.Info);
        };
        CreateTrayIcon();
        await viewModel.InitializeAsync();
        _mainWindow.Show();
        _mainWindow.Activate();
        if (hasSavedDynamicWallpaper)
            _ = RestoreDynamicWallpaperHostAsync(_lifetime.Token);
        _ = RunUpdateChecksAsync(_lifetime.Token);
    }

    private void CreateTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("打开 Wallpaper Studio", null, (_, _) => ShowMainWindow());
        _updateMenuItem = new WinForms.ToolStripMenuItem("检查更新");
        _updateMenuItem.Click += async (_, _) => await CheckForUpdatesAsync(manual: true);
        menu.Items.Add(_updateMenuItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());
        _appIcon = Icon.ExtractAssociatedIcon(ApplicationExecutable.CurrentPath);
        _trayIcon = new WinForms.NotifyIcon
        {
            Text = "Wallpaper Studio",
            Icon = _appIcon ?? SystemIcons.Application,
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized) _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true; _trayIcon?.Dispose(); _appIcon?.Dispose(); _mainWindow?.Close(); Shutdown();
    }

    private async Task RestoreDynamicWallpaperHostAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _hostClient!.EnsureRunningAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (_paths is not null)
                await File.AppendAllTextAsync(_paths.Log,
                    $"{DateTime.Now:O} 更新或启动后恢复动态壁纸宿主失败: {exception}\n");
        }
    }

    private async Task RunUpdateChecksAsync(CancellationToken cancellationToken)
    {
        if (_updateService is not { IsInstalled: true }) return;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                await CheckForUpdatesAsync(manual: false);
                await Task.Delay(TimeSpan.FromHours(6), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_updateService is null || _paths is null) return;
        if (_updateMenuItem is not null) _updateMenuItem.Enabled = false;
        try
        {
            var result = await _updateService.CheckAndDownloadAsync(_lifetime?.Token ?? CancellationToken.None);
            switch (result.Status)
            {
                case UpdateCheckStatus.Unsupported when manual:
                    MessageBox.Show("自动更新仅在安装版中启用。", "检查更新",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case UpdateCheckStatus.UpToDate when manual:
                    MessageBox.Show($"当前已是最新版本（{result.Version ?? "未知版本"}）。", "检查更新",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case UpdateCheckStatus.ReadyToInstall:
                    await PromptToInstallUpdateAsync(result.Version, force: manual);
                    break;
            }
        }
        catch (OperationCanceledException) when (_lifetime?.IsCancellationRequested == true)
        {
        }
        catch (Exception exception)
        {
            await File.AppendAllTextAsync(_paths.Log, $"{DateTime.Now:O} 检查或下载更新失败: {exception}\n");
            if (manual)
                MessageBox.Show("暂时无法检查更新，请稍后重试。", "检查更新",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            if (_updateMenuItem is not null) _updateMenuItem.Enabled = true;
        }
    }

    private async Task PromptToInstallUpdateAsync(string? version, bool force)
    {
        version ??= _updateService?.PendingVersion ?? "新版本";
        if (!force && string.Equals(_lastPromptedUpdate, version, StringComparison.Ordinal)) return;
        _lastPromptedUpdate = version;
        if (_updateMenuItem is not null) _updateMenuItem.Text = $"安装已下载更新 {version}";

        var result = MessageBox.Show(
            $"Wallpaper Studio {version} 已下载完成。\n\n是否立即重启并安装？动态壁纸会短暂停止，更新完成后自动恢复。",
            "更新已准备好", MessageBoxButton.YesNo, MessageBoxImage.Information);
        if (result == MessageBoxResult.Yes)
            await ApplyPendingUpdateAsync();
    }

    private async Task ApplyPendingUpdateAsync()
    {
        try
        {
            if (_hostClient is not null) await _hostClient.StopAsync();
            _updateService!.ApplyAndRestart();
            _isExiting = true;
            _lifetime?.Cancel();
            _trayIcon?.Dispose();
            _appIcon?.Dispose();
            _mainWindow?.Close();
            Shutdown();
        }
        catch (Exception exception)
        {
            MessageBox.Show($"暂时无法安装更新：{exception.Message}", "更新失败",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RunWallpaperHostAsync()
    {
        try
        {
            await _hostServer!.RunAsync();
        }
        catch (Exception exception)
        {
            if (_paths is not null)
                await File.AppendAllTextAsync(_paths.Log, $"{DateTime.Now:O} {exception}\n");
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _lifetime?.Cancel();
        _lifetime?.Dispose();
        _hostServer?.Dispose();
        base.OnExit(e);
    }
}

