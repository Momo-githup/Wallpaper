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
    private bool _isExiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _paths = new AppPaths();
        DispatcherUnhandledException += (_, args) =>
        {
            File.AppendAllText(_paths.Log, $"{DateTime.Now:O} {args.Exception}\n");
            MessageBox.Show("发生了未处理的错误，详情已写入日志。", "Wallpaper Studio", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
        var images = new ImageService(_paths);
        new StartupService().EnsureDisabled();
        var viewModel = new MainViewModel(new LibraryDatabase(_paths), images, new StaticWallpaperEngine(images), new SettingsService(_paths));
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
}

