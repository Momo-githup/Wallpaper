using WallpaperStudio.App.Models;

namespace WallpaperStudio.App.Services;

public interface IAuthService
{
    bool IsAuthenticated { get; }
    string? CurrentUserEmail { get; }
    string? CurrentDisplayName { get; }

    Task InitializeAsync();
    Task SignInAsync(string email, string password);
    Task<SignUpResult> SignUpAsync(string email, string password);
    Task VerifySignUpOtpAsync(string email, string code);
    Task UpdateDisplayNameAsync(string displayName);
    Task SignOutAsync();
}
