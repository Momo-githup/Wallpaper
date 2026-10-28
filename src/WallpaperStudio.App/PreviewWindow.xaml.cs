using System.Windows;
using System.Windows.Input;
using WallpaperStudio.App.Services;

namespace WallpaperStudio.App;

public partial class PreviewWindow : Window
{
    public PreviewWindow(string path) { InitializeComponent(); PreviewImage.Source = ImageService.LoadBitmap(path); }
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => Close();
}
