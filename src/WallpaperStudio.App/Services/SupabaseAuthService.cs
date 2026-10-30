using Supabase.Gotrue;
using Supabase.Gotrue.Exceptions;
using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

public sealed class SupabaseAuthService : IAuthService
{
    private readonly Supabase.Client _client;
    private readonly SupabaseClientProvider _provider;

    public SupabaseAuthService(SupabaseClientProvider provider)
    {
        _provider = provider;
        _client = provider.Client;
    }

    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(_client.Auth.CurrentSession?.AccessToken);
    public string? CurrentUserEmail => _client.Auth.CurrentUser?.Email;

    public async Task InitializeAsync()
    {
        await _provider.InitializeAsync();
    }

    public async Task SignInAsync(string email, string password)
    {
        await EnsureInitializedAsync();
        try
        {
            var session = await _client.Auth.SignIn(email.Trim(), password);
            if (string.IsNullOrWhiteSpace(session?.AccessToken))
                throw new InvalidOperationException("登录失败，请先注册账号或检查密码。");
        }
        catch (GotrueException exception) when (
            exception.Reason is FailureHint.Reason.UserBadLogin or FailureHint.Reason.UserBadPassword ||
            string.Equals(exception.ErrorCode, "invalid_credentials", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("登录失败，请先注册账号或检查邮箱和密码。", exception);
        }
        catch (GotrueException exception) when (
            exception.Reason is FailureHint.Reason.UserEmailNotConfirmed ||
            string.Equals(exception.ErrorCode, "email_not_confirmed", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("账号尚未完成注册，请点击注册并输入邮箱收到的验证码。", exception);
        }
    }

    public async Task<SignUpResult> SignUpAsync(string email, string password)
    {
        await EnsureInitializedAsync();
        var displayName = $"用户_{Guid.NewGuid():N}"[..9].ToUpperInvariant();
        var options = new SignUpOptions
        {
            Data = new Dictionary<string, object>
            {
                ["display_name"] = displayName
            }
        };
        var session = await _client.Auth.SignUp(email.Trim(), password, options);
        var signedIn = !string.IsNullOrWhiteSpace(session?.AccessToken);
        return new SignUpResult(
            signedIn,
            signedIn ? "注册成功，已登录。" : "验证码已发送，请填写邮件中的验证码完成注册。");
    }

    public async Task VerifySignUpOtpAsync(string email, string code)
    {
        await EnsureInitializedAsync();
        var session = await _client.Auth.VerifyOTP(email.Trim(), code.Trim(), Constants.EmailOtpType.Signup);
        if (string.IsNullOrWhiteSpace(session?.AccessToken))
            throw new InvalidOperationException("验证码验证成功，但未返回有效登录会话。");
    }

    public async Task SignOutAsync()
    {
        await _provider.InitializeAsync();
        if (_client.Auth.CurrentSession is not null)
            await _client.Auth.SignOut(Constants.SignOutScope.Local);
    }

    private async Task EnsureInitializedAsync()
    {
        await _provider.InitializeAsync();
    }
}
