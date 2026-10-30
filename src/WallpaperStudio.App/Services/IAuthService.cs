using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

public interface IAuthService
{
    bool IsAuthenticated { get; }
    string? CurrentUserEmail { get; }

    Task InitializeAsync();
    Task SignInAsync(string email, string password);
    Task<SignUpResult> SignUpAsync(string email, string password);
    Task VerifySignUpOtpAsync(string email, string code);
    Task SignOutAsync();
}
