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
    private MainWindow? _mainWindow;
    private AppPaths? _paths;
    private DynamicWallpaperHostServer? _hostServer;
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
        var wallpaperManager = new WallpaperManager(new StaticWallpaperEngine(images), new DynamicWallpaperHostClient());
        var supabase = new SupabaseClientProvider(_paths);
        var authService = new SupabaseAuthService(supabase);
        var communityService = new SupabaseCommunityService(supabase, images);
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
    }

    private void CreateTrayIcon()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add("打开 Wallpaper Studio", null, (_, _) => ShowMainWindow());
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => ExitApplication());
        _trayIcon = new WinForms.NotifyIcon { Text = "Wallpaper Studio", Icon = SystemIcons.Application, Visible = true, ContextMenuStrip = menu };
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
        _isExiting = true; _trayIcon?.Dispose(); _mainWindow?.Close(); Shutdown();
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
        _hostServer?.Dispose();
        base.OnExit(e);
    }
}

