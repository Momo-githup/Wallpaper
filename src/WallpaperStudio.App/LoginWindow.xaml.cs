using System.Net.Http;
using System.Windows;
using WallpaperStudio.App.ViewModels;

namespace WallpaperStudio.App;

public partial class LoginWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _awaitingVerification;

    public LoginWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateCommonFields()) return;
        await RunAsync(async () =>
        {
            await _viewModel.SignInAsync(EmailBox.Text, PasswordBox.Password);
            DialogResult = true;
        });
    }

    private async void SignUp_Click(object sender, RoutedEventArgs e)
    {
        if (_awaitingVerification)
        {
            await CompleteRegistrationAsync();
            return;
        }

        if (!ValidateCommonFields()) return;
        await RunAsync(async () =>
        {
            var result = await _viewModel.SignUpAsync(EmailBox.Text, PasswordBox.Password);
            if (result.IsSignedIn)
            {
                DialogResult = true;
                return;
            }

            MessageText.Foreground = System.Windows.Media.Brushes.LightGreen;
            MessageText.Text = "验证码已发送，请输入邮箱收到的验证码。";
            _awaitingVerification = true;
            VerificationPanel.Visibility = Visibility.Visible;
            SignUpButton.Content = "完成注册";
            EmailBox.IsReadOnly = true;
            PasswordBox.IsEnabled = false;
            VerificationCodeBox.Focus();
        });
    }

    private async Task CompleteRegistrationAsync()
    {
        if (!ValidateEmail()) return;
        var code = VerificationCodeBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            MessageText.Text = "请输入邮件中的验证码。";
            return;
        }

        await RunAsync(async () =>
        {
            await _viewModel.VerifySignUpOtpAsync(EmailBox.Text, code);
            DialogResult = true;
        });
    }

    private bool ValidateCommonFields()
    {
        MessageText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 138, 128));
        if (!ValidateEmail()) return false;

        if (PasswordBox.Password.Length < 6)
        {
            MessageText.Text = "密码至少需要 6 个字符。";
            return false;
        }

        return true;
    }

    private bool ValidateEmail()
    {
        MessageText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 138, 128));
        if (string.IsNullOrWhiteSpace(EmailBox.Text) || !EmailBox.Text.Contains('@'))
        {
            MessageText.Text = "请输入有效邮箱地址。";
            return false;
        }
        return true;
    }

    private async Task RunAsync(Func<Task> action)
    {
        SetBusy(true);
        MessageText.Text = string.Empty;
        try
        {
            await action();
        }
        catch (HttpRequestException)
        {
            MessageText.Text = "无法连接登录服务，请检查网络后重试。";
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

    private void SetBusy(bool busy)
    {
        SignInButton.IsEnabled = !busy && !_awaitingVerification;
        SignUpButton.IsEnabled = !busy;
        EmailBox.IsEnabled = !busy;
        PasswordBox.IsEnabled = !busy && !_awaitingVerification;
        VerificationCodeBox.IsEnabled = !busy;
    }
}
