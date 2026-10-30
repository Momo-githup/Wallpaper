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
    private readonly MediaFileService _mediaFiles;
    private readonly WallpaperManager _wallpaperManager;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;
    private readonly IAuthService _authService;
    private readonly ICommunityService _communityService;
    private AppSettings _settings = new();

    public MainViewModel(LibraryDatabase database, ImageService images, MediaFileService mediaFiles, WallpaperManager wallpaperManager,
        SettingsService settingsService, StartupService startupService, IAuthService authService,
        ICommunityService communityService)
    {
        _database = database;
        _images = images;
        _mediaFiles = mediaFiles;
        _wallpaperManager = wallpaperManager;
        _settingsService = settingsService;
        _startupService = startupService;
        _authService = authService;
        _communityService = communityService;
    }

    public ObservableCollection<WallpaperCardViewModel> Wallpapers { get; } = [];
    public ObservableCollection<CommunityWallpaperItem> CommunityWallpapers { get; } = [];
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyWallpaperCommand), nameof(RemoveCommand), nameof(PreviewCommand), nameof(PublishSelectedWallpaperCommand))]
    private WallpaperCardViewModel? selectedWallpaper;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DownloadCommunityWallpaperCommand), nameof(WithdrawCommunityWallpaperCommand))]
    private CommunityWallpaperItem? selectedCommunityWallpaper;

    [ObservableProperty]
    private bool isCommunityView;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "准备就绪";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShowCommunityCommand), nameof(RefreshCommunityCommand), nameof(PublishSelectedWallpaperCommand))]
    private bool isAuthenticated;

    [ObservableProperty]
    private string currentUserEmail = string.Empty;

    [ObservableProperty]
    private string currentUserName = string.Empty;

    public event Func<string, WallpaperMediaType, Task>? PreviewRequested;

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            await _database.InitializeAsync();
            _settings = await _settingsService.LoadAsync();
            try
            {
                await _authService.InitializeAsync();
                RefreshAuthState();
            }
            catch
            {
                IsAuthenticated = false;
                CurrentUserEmail = string.Empty;
            }
            await ReloadAsync();
            StatusMessage = Wallpapers.Count == 0 ? "导入几张喜欢的图片开始吧" : $"已加载 {Wallpapers.Count} 张壁纸";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SignInAsync(string email, string password)
    {
        await _authService.SignInAsync(email, password);
        RefreshAuthState();
        StatusMessage = $"已登录：{CurrentUserName}";
    }

    public async Task<SignUpResult> SignUpAsync(string email, string password)
    {
        var result = await _authService.SignUpAsync(email, password);
        RefreshAuthState();
        StatusMessage = result.Message;
        return result;
    }

    public async Task VerifySignUpOtpAsync(string email, string code)
    {
        await _authService.VerifySignUpOtpAsync(email, code);
        RefreshAuthState();
        StatusMessage = $"注册成功，已登录：{CurrentUserName}";
    }

    public async Task UpdateDisplayNameAsync(string displayName)
    {
        await _authService.UpdateDisplayNameAsync(displayName);
        RefreshAuthState();
        StatusMessage = $"用户名称已更新：{CurrentUserName}";
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        try
        {
            await _authService.SignOutAsync();
            RefreshAuthState();
            StatusMessage = "已退出账户，本地图库仍可正常使用";
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "退出登录失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshAuthState()
    {
        IsAuthenticated = _authService.IsAuthenticated;
        CurrentUserEmail = _authService.CurrentUserEmail ?? string.Empty;
        CurrentUserName = _authService.CurrentDisplayName ?? CurrentUserEmail.Split('@')[0];
        if (!IsAuthenticated)
        {
            CurrentUserName = string.Empty;
            IsCommunityView = false;
            CommunityWallpapers.Clear();
            SelectedCommunityWallpaper = null;
        }
    }

    [RelayCommand]
    private void ShowLibrary()
    {
        IsCommunityView = false;
        StatusMessage = Wallpapers.Count == 0 ? "导入几张喜欢的图片开始吧" : $"已加载 {Wallpapers.Count} 张壁纸";
    }

    private bool CanUseCommunity() => IsAuthenticated;

    [RelayCommand(CanExecute = nameof(CanUseCommunity))]
    private async Task ShowCommunityAsync()
    {
        IsCommunityView = true;
        await LoadCommunityAsync();
    }

    [RelayCommand(CanExecute = nameof(CanUseCommunity))]
    private async Task RefreshCommunityAsync() => await LoadCommunityAsync();

    private bool CanPublishSelectedWallpaper() =>
        IsAuthenticated && SelectedWallpaper is not null &&
        (SelectedWallpaper.Item.MediaType == WallpaperMediaType.Image ||
         Path.GetExtension(SelectedWallpaper.FilePath).Equals(".mp4", StringComparison.OrdinalIgnoreCase));

    [RelayCommand(CanExecute = nameof(CanPublishSelectedWallpaper))]
    private async Task PublishSelectedWallpaperAsync()
    {
        if (SelectedWallpaper is null) return;
        IsBusy = true;
        try
        {
            await _communityService.PublishAsync(SelectedWallpaper.Item);
            IsCommunityView = true;
            await ReloadCommunityAsync();
            StatusMessage = $"已发布到社区：{SelectedWallpaper.DisplayName}";
        }
        catch (Exception exception)
        {
            StatusMessage = "发布失败";
            MessageBox.Show(exception.Message, "发布到社区失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool HasCommunitySelection() => SelectedCommunityWallpaper is not null;

    private bool CanWithdrawCommunityWallpaper() => SelectedCommunityWallpaper?.IsOwnedByCurrentUser == true;

    [RelayCommand(CanExecute = nameof(HasCommunitySelection))]
    private async Task DownloadCommunityWallpaperAsync()
    {
        if (SelectedCommunityWallpaper is null) return;
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "保存并导入社区壁纸",
            FileName = SelectedCommunityWallpaper.SuggestedFileName,
            Filter = SelectedCommunityWallpaper.IsVideo
                ? "MP4 视频|*.mp4|所有文件|*.*"
                : "图片文件|*.jpg;*.jpeg;*.png;*.webp|所有文件|*.*"
        };
        if (dialog.ShowDialog() != true) return;

        IsBusy = true;
        try
        {
            var downloadedPath = Path.GetFullPath(dialog.FileName);
            var title = SelectedCommunityWallpaper.Title;
            await _communityService.DownloadAsync(SelectedCommunityWallpaper, downloadedPath);
            await ImportPathsCoreAsync([downloadedPath]);
            SelectedWallpaper = Wallpapers.FirstOrDefault(item =>
                item.FilePath.Equals(downloadedPath, StringComparison.OrdinalIgnoreCase));
            if (SelectedWallpaper is null)
                throw new InvalidDataException("壁纸已下载，但文件无法导入本地图库。");
            IsCommunityView = false;
            StatusMessage = $"已下载并导入：{title}";
        }
        catch (Exception exception)
        {
            StatusMessage = "下载失败";
            MessageBox.Show(exception.Message, "下载社区壁纸失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanWithdrawCommunityWallpaper))]
    private async Task WithdrawCommunityWallpaperAsync()
    {
        if (SelectedCommunityWallpaper is not { IsOwnedByCurrentUser: true } wallpaper) return;
        var confirmation = MessageBox.Show(
            $"确定要撤下“{wallpaper.Title}”吗？\n撤下后将从社区隐藏，并删除云端文件。",
            "撤下社区壁纸",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmation != MessageBoxResult.Yes) return;

        IsBusy = true;
        try
        {
            var filesDeleted = await _communityService.WithdrawAsync(wallpaper);
            await ReloadCommunityAsync();
            StatusMessage = filesDeleted
                ? $"已撤下：{wallpaper.Title}"
                : $"已撤下：{wallpaper.Title}（云端文件清理失败）";
        }
        catch (Exception exception)
        {
            StatusMessage = "撤下失败";
            MessageBox.Show(exception.Message, "撤下社区壁纸失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadCommunityAsync()
    {
        IsBusy = true;
        try
        {
            await ReloadCommunityAsync();
            StatusMessage = CommunityWallpapers.Count == 0
                ? "社区暂时还没有壁纸，发布第一张吧"
                : $"社区已加载 {CommunityWallpapers.Count} 张壁纸";
        }
        catch (Exception exception)
        {
            StatusMessage = "加载社区失败";
            MessageBox.Show(exception.Message, "壁纸社区", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ReloadCommunityAsync()
    {
        var selectedId = SelectedCommunityWallpaper?.Id;
        var items = await _communityService.GetFeedAsync();
        CommunityWallpapers.Clear();
        foreach (var item in items) CommunityWallpapers.Add(item);
        SelectedCommunityWallpaper = CommunityWallpapers.FirstOrDefault(item => item.Id == selectedId)
            ?? CommunityWallpapers.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ImportImagesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择图片壁纸",
            Filter = "图片壁纸|*.jpg;*.jpeg;*.png;*.bmp;*.webp|所有文件|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true) await ImportPathsAsync(dialog.FileNames);
    }

    [RelayCommand]
    private async Task ImportVideosAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择视频壁纸",
            Filter = "视频壁纸|*.mp4;*.wmv;*.avi;*.mov;*.m4v|MP4 视频|*.mp4|所有文件|*.*",
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
            .Where(_mediaFiles.IsSupported);
        await ImportPathsAsync(files);
    }

    public async Task ImportPathsAsync(IEnumerable<string> paths)
    {
        IsBusy = true;
        try
        {
            var added = await ImportPathsCoreAsync(paths);
            StatusMessage = added == 0 ? "没有发现新的有效壁纸" : $"已导入 {added} 个壁纸";
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

    private async Task<int> ImportPathsCoreAsync(IEnumerable<string> paths)
    {
        var valid = new List<(string Path, int Width, int Height, long Size, WallpaperMediaType MediaType)>();
        foreach (var candidate in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.GetFullPath(candidate);
            if (!_mediaFiles.IsSupported(path) || !File.Exists(path)) continue;
            var info = await _mediaFiles.InspectAsync(path);
            if (info is { } mediaInfo)
                valid.Add((path, mediaInfo.Width, mediaInfo.Height, mediaInfo.Size, _mediaFiles.GetMediaType(path)));
        }
        var added = await _database.AddAsync(valid);
        await ReloadAsync();
        return added;
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
            await _wallpaperManager.ApplyAsync(SelectedWallpaper.Item);
            _settings.CurrentWallpaperPath = SelectedWallpaper.FilePath;
            _settings.CurrentWallpaperMediaType = SelectedWallpaper.Item.MediaType;
            await _settingsService.SaveAsync(_settings);
            if (SelectedWallpaper.Item.MediaType == WallpaperMediaType.Video)
                _startupService.EnableDynamicHost();
            else
                _startupService.Disable();
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
        var type = SelectedWallpaper.Item.MediaType;
        var path = type == WallpaperMediaType.Image
            ? await _images.PrepareDisplayFileAsync(SelectedWallpaper.FilePath)
            : SelectedWallpaper.FilePath;
        await PreviewRequested.Invoke(path, type);
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
