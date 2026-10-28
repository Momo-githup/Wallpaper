using System.Windows;
using System.Windows.Input;
using WallpaperStudio.App.Services;
using WallpaperStudio.App.Models;

namespace WallpaperStudio.App;

public partial class PreviewWindow : Window
{
    public PreviewWindow(string path, WallpaperMediaType mediaType)
    {
        InitializeComponent();
        if (mediaType == WallpaperMediaType.Video)
        {
            PreviewImage.Visibility = Visibility.Collapsed;
            PreviewVideo.Visibility = Visibility.Visible;
            PreviewVideo.Source = new Uri(path, UriKind.Absolute);
            PreviewVideo.Play();
        }
        else
        {
            PreviewImage.Source = ImageService.LoadBitmap(path);
        }
        Closed += (_, _) => { PreviewVideo.Stop(); PreviewVideo.Source = null; };
    }
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
    private void Close_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Close();
    private void PreviewVideo_MediaEnded(object sender, RoutedEventArgs e) { PreviewVideo.Position = TimeSpan.Zero; PreviewVideo.Play(); }
}
