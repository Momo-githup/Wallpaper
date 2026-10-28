using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;
using WallpaperStudio.App.Models;
using WallpaperStudio.App.Services;

namespace WallpaperStudio.App.ViewModels;

public sealed partial class WallpaperCardViewModel : ObservableObject
{
    private readonly ImageService _images;

    public WallpaperCardViewModel(WallpaperItem item, ImageService images)
    {
        Item = item;
        _images = images;
    }

    public WallpaperItem Item { get; }
    public long Id => Item.Id;
    public string FilePath => Item.FilePath;
    public string DisplayName => Item.DisplayName;
    public string Resolution => Item.Resolution;
    public string FileSizeText => Item.FileSizeText;
    public bool Exists => File.Exists(FilePath);

    [ObservableProperty]
    private ImageSource? thumbnail;

    public async Task LoadThumbnailAsync() => Thumbnail = await _images.GetThumbnailAsync(FilePath);
}
