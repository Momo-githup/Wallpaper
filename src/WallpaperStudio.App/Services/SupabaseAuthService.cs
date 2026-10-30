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
    public string? CurrentDisplayName { get; private set; }

    public async Task InitializeAsync()
    {
        await _provider.InitializeAsync();
        await LoadCurrentProfileAsync();
    }

    public async Task SignInAsync(string email, string password)
    {
        await EnsureInitializedAsync();
        try
        {
            var session = await _client.Auth.SignIn(email.Trim(), password);
            if (string.IsNullOrWhiteSpace(session?.AccessToken))
                throw new InvalidOperationException("登录失败，请先注册账号或检查密码。");
            await LoadCurrentProfileAsync();
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
        if (signedIn) await LoadCurrentProfileAsync();
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
        await LoadCurrentProfileAsync();
    }

    public async Task UpdateDisplayNameAsync(string displayName)
    {
        await EnsureInitializedAsync();
        var userId = _client.Auth.CurrentUser?.Id
            ?? throw new InvalidOperationException("登录状态已失效，请重新登录。");
        var value = displayName.Trim();
        if (value.Length is < 1 or > 50)
            throw new InvalidOperationException("用户名称需要包含 1 到 50 个字符。");

        var response = await _client.From<CommunityProfileRecord>()
            .Set(profile => profile.DisplayName, value)
            .Where(profile => profile.Id == userId)
            .Update();
        if (response.Models.Count == 0)
            throw new InvalidOperationException("用户资料不存在，无法更新名称。");
        CurrentDisplayName = response.Models[0].DisplayName;
    }

    public async Task SignOutAsync()
    {
        await _provider.InitializeAsync();
        if (_client.Auth.CurrentSession is not null)
            await _client.Auth.SignOut(Constants.SignOutScope.Local);
        CurrentDisplayName = null;
    }

    private async Task EnsureInitializedAsync()
    {
        await _provider.InitializeAsync();
    }

    private async Task LoadCurrentProfileAsync()
    {
        if (!IsAuthenticated || _client.Auth.CurrentUser?.Id is not { } userId)
        {
            CurrentDisplayName = null;
            return;
        }

        var profile = await _client.From<CommunityProfileRecord>()
            .Where(item => item.Id == userId)
            .Single();
        CurrentDisplayName = string.IsNullOrWhiteSpace(profile?.DisplayName)
            ? CurrentUserEmail?.Split('@')[0]
            : profile.DisplayName;
    }
}
