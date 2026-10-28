using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using WallpaperStudio.App.Models;
using WallpaperStudio.App.Services;
using WinForms = System.Windows.Forms;

namespace WallpaperStudio.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly LibraryDatabase _database;
    private readonly ImageService _images;
    private readonly IWallpaperEngine _wallpaperEngine;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;
    private AppSettings _settings = new();

    public MainViewModel(LibraryDatabase database, ImageService images, IWallpaperEngine wallpaperEngine,
        SettingsService settingsService, StartupService startupService)
    {
        _database = database;
        _images = images;
        _wallpaperEngine = wallpaperEngine;
        _settingsService = settingsService;
        _startupService = startupService;
    }

    public ObservableCollection<WallpaperCardViewModel> Wallpapers { get; } = [];
    public IReadOnlyList<WallpaperLayout> Layouts { get; } = Enum.GetValues<WallpaperLayout>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyWallpaperCommand), nameof(RemoveCommand), nameof(PreviewCommand))]
    private WallpaperCardViewModel? selectedWallpaper;

    [ObservableProperty]
    private WallpaperLayout selectedLayout = WallpaperLayout.Fill;

    [ObservableProperty]
    private bool startWithWindows;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "准备就绪";

    public event Func<string, Task>? PreviewRequested;

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await _database.InitializeAsync();
            _settings = await _settingsService.LoadAsync();
            SelectedLayout = _settings.Layout;
            StartWithWindows = _startupService.IsEnabled;
            await ReloadAsync();
            StatusMessage = Wallpapers.Count == 0 ? "导入几张喜欢的图片开始吧" : $"已加载 {Wallpapers.Count} 张壁纸";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportFilesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择壁纸",
            Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.webp|所有文件|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true) await ImportPathsAsync(dialog.FileNames);
    }

    [RelayCommand]
    private async Task ImportFolderAsync()
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description = "选择包含壁纸的文件夹",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;
        var files = Directory.EnumerateFiles(dialog.SelectedPath, "*.*", SearchOption.AllDirectories)
            .Where(_images.IsSupported);
        await ImportPathsAsync(files);
    }

    public async Task ImportPathsAsync(IEnumerable<string> paths)
    {
        IsBusy = true;
        try
        {
            var valid = new List<(string Path, int Width, int Height, long Size)>();
            foreach (var candidate in paths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var path = Path.GetFullPath(candidate);
                if (!_images.IsSupported(path) || !File.Exists(path)) continue;
                var info = await _images.InspectAsync(path);
                if (info is { } imageInfo) valid.Add((path, imageInfo.Width, imageInfo.Height, imageInfo.Size));
            }
            var added = await _database.AddAsync(valid);
            await ReloadAsync();
            StatusMessage = added == 0 ? "没有发现新的有效图片" : $"已导入 {added} 张壁纸";
        }
        catch (Exception exception)
        {
            StatusMessage = "导入失败";
            MessageBox.Show(exception.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool HasSelection() => SelectedWallpaper is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task ApplyWallpaperAsync()
    {
        if (SelectedWallpaper is null) return;
        if (!SelectedWallpaper.Exists)
        {
            MessageBox.Show("原始文件已被移动或删除。", "文件不存在", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        IsBusy = true;
        try
        {
            await _wallpaperEngine.ApplyAsync(SelectedWallpaper.FilePath, SelectedLayout);
            _settings.Layout = SelectedLayout;
            _settings.CurrentWallpaperPath = SelectedWallpaper.FilePath;
            await _settingsService.SaveAsync(_settings);
            StatusMessage = $"已应用：{SelectedWallpaper.DisplayName}";
        }
        catch (Exception exception)
        {
            StatusMessage = "设置壁纸失败";
            MessageBox.Show(exception.Message, "设置失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RemoveAsync()
    {
        if (SelectedWallpaper is null) return;
        await _database.RemoveAsync(SelectedWallpaper.Id);
        Wallpapers.Remove(SelectedWallpaper);
        SelectedWallpaper = null;
        StatusMessage = "已从图库移除，原文件未删除";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task PreviewAsync()
    {
        if (SelectedWallpaper is null || PreviewRequested is null) return;
        var path = await _images.PrepareDisplayFileAsync(SelectedWallpaper.FilePath);
        await PreviewRequested.Invoke(path);
    }

    [RelayCommand]
    private async Task SavePreferencesAsync()
    {
        try
        {
            _startupService.SetEnabled(StartWithWindows);
            _settings.StartWithWindows = StartWithWindows;
            _settings.Layout = SelectedLayout;
            await _settingsService.SaveAsync(_settings);
            StatusMessage = "设置已保存";
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "保存设置失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ReloadAsync()
    {
        Wallpapers.Clear();
        foreach (var item in await _database.GetAllAsync()) Wallpapers.Add(new WallpaperCardViewModel(item, _images));
        SelectedWallpaper = Wallpapers.FirstOrDefault(x => x.FilePath.Equals(_settings.CurrentWallpaperPath, StringComparison.OrdinalIgnoreCase))
            ?? Wallpapers.FirstOrDefault();
        await Task.WhenAll(Wallpapers.Select(x => x.LoadThumbnailAsync()));
    }
}
