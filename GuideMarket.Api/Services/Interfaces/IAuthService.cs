using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IAuthService
{
    Task<RegisterResponse> RegisterAsync(RegisterRequest request);
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<LoginResponse> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(string accessToken);
    Task ForgotPasswordAsync(string email);
    Task ResetPasswordAsync(ResetPasswordRequest request);

    Task<VerifyEmailResponse> VerifyEmailAsync(string email, string otp);
    Task ResendVerifyEmailAsync(string email);

    Task RequestOtpAsync(string phone);
    Task VerifyPhoneAsync(Guid userId, string phone, string token);

    /// <summary>Tạo/cập nhật user từ Supabase webhook (email confirmed hoặc OAuth sign-up).</summary>
    Task CreateUserFromWebhookAsync(Guid userId, string email, string fullName, string? avatarUrl = null);

    /// <summary>Trả về OAuth URL để frontend redirect user đến Google login.</summary>
    string GetGoogleOAuthUrl(string? redirectTo);

    /// <summary>Sau khi OAuth hoàn tất, đồng bộ user vào local DB và trả về profile.</summary>
    Task<LoginResponse> HandleSocialLoginAsync(Guid userId);
}
