using Supabase.Gotrue;
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
        var session = await _client.Auth.SignIn(email.Trim(), password);
        if (string.IsNullOrWhiteSpace(session?.AccessToken))
            throw new InvalidOperationException("登录未返回有效会话，请确认邮箱已经验证。 ");
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
