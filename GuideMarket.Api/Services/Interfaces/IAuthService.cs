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

    /// <summary>Tạo/cập nhật user từ Supabase webhook (email confirmed).</summary>
    Task CreateUserFromWebhookAsync(Guid userId, string email, string fullName);
}
