using System.Net.Http;
using System.Windows;
using WallpaperStudio.App.ViewModels;

namespace WallpaperStudio.App;

public partial class AccountWindow : Window
{
    private readonly MainViewModel _viewModel;

    public AccountWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        EmailBox.Text = viewModel.CurrentUserEmail;
        DisplayNameBox.Text = viewModel.CurrentUserName;
        Loaded += (_, _) =>
        {
            DisplayNameBox.Focus();
            DisplayNameBox.SelectAll();
        };
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var displayName = DisplayNameBox.Text.Trim();
        if (displayName.Length is < 1 or > 50)
        {
            MessageText.Text = "用户名称需要包含 1 到 50 个字符。";
            return;
        }

        SetBusy(true);
        MessageText.Text = string.Empty;
        try
        {
            await _viewModel.UpdateDisplayNameAsync(displayName);
            DialogResult = true;
        }
        catch (HttpRequestException)
        {
            MessageText.Text = "无法连接账户服务，请检查网络后重试。";
        }
        catch (Exception exception)
        {
            MessageText.Text = exception.Message;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();

    private void SetBusy(bool busy)
    {
        SaveButton.IsEnabled = !busy;
        DisplayNameBox.IsEnabled = !busy;
    }
}
