using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WallpaperStudio.App;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly WallpaperStudio.App.ViewModels.MainViewModel _viewModel;

    public MainWindow(WallpaperStudio.App.ViewModels.MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        _viewModel.PreviewRequested += ShowPreviewAsync;
    }

    private Task ShowPreviewAsync(string path, WallpaperStudio.App.Models.WallpaperMediaType mediaType)
    {
        new PreviewWindow(path, mediaType) { Owner = this }.ShowDialog();
        return Task.CompletedTask;
    }

    private void Login_Click(object sender, RoutedEventArgs e)
    {
        new LoginWindow(_viewModel) { Owner = this }.ShowDialog();
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        var files = paths.SelectMany(path => Directory.Exists(path)
            ? Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
            : [path]);
        await _viewModel.ImportPathsAsync(files);
    }
}
