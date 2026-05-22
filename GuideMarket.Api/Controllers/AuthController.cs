using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>Đăng ký tài khoản mới. Backend gửi OTP xác thực qua email.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        return StatusCode(201, ApiResponse<RegisterResponse>.Ok(result, result.Message));
    }

    /// <summary>Đăng nhập. Trả về access_token và refresh_token.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Login successful"));
    }

    /// <summary>Làm mới access_token bằng refresh_token.</summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request.RefreshToken);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Token refreshed"));
    }

    /// <summary>Đăng xuất — invalidate token phía Supabase.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var accessToken = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
        await _authService.LogoutAsync(accessToken);
        return Ok(ApiResponse<object>.Ok(null!, "Logged out successfully"));
    }

    /// <summary>Gửi email reset mật khẩu.</summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        await _authService.ForgotPasswordAsync(request.Email);
        // Luôn trả 200 dù email có tồn tại hay không (tránh email enumeration)
        return Ok(ApiResponse<object>.Ok(null!,
            "If this email exists, a password reset link has been sent."));
    }

    /// <summary>Đặt lại mật khẩu bằng OTP (email + mã 6 số + mật khẩu mới).</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request);
        return Ok(ApiResponse<object>.Ok(null!, "Đặt lại mật khẩu thành công. Vui lòng đăng nhập."));
    }

    /// <summary>Xác nhận email bằng OTP 6 số.</summary>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await _authService.VerifyEmailAsync(request.Email, request.Otp);
        return Ok(ApiResponse<VerifyEmailResponse>.Ok(result, result.Message));
    }

    /// <summary>Gửi lại OTP xác nhận đăng ký.</summary>
    [HttpPost("resend-verify-email")]
    public async Task<IActionResult> ResendVerifyEmail([FromBody] ResendVerifyEmailRequest request)
    {
        await _authService.ResendVerifyEmailAsync(request.Email);
        return Ok(ApiResponse<object>.Ok(null!, "Verification email/OTP resent"));
    }

    /// <summary>Gửi OTP xác thực số điện thoại.</summary>
    [HttpPost("request-otp")]
    [Authorize]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest request)
    {
        await _authService.RequestOtpAsync(request.Phone);
        return Ok(ApiResponse<object>.Ok(null!, "OTP sent to your phone number"));
    }

    /// <summary>Xác thực OTP và lưu số điện thoại vào profile.</summary>
    [HttpPost("verify-phone")]
    [Authorize]
    public async Task<IActionResult> VerifyPhone([FromBody] VerifyPhoneRequest request)
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token"));

        await _authService.VerifyPhoneAsync(userId, request.Phone, request.Token);
        return Ok(ApiResponse<object>.Ok(null!, "Phone number verified successfully"));
    }

    /// <summary>
    /// Trả về URL để frontend redirect user đến trang đăng nhập Google.
    /// Sau khi Google xác thực, Supabase sẽ redirect về redirectTo với access_token trong URL hash.
    /// </summary>
    [HttpGet("google")]
    public IActionResult GetGoogleOAuthUrl([FromQuery] string? redirectTo)
    {
        var url = _authService.GetGoogleOAuthUrl(redirectTo);
        return Ok(ApiResponse<GoogleOAuthUrlResponse>.Ok(
            new GoogleOAuthUrlResponse { Url = url, Provider = "google" },
            "Redirect user to this URL to sign in with Google"));
    }

    /// <summary>
    /// Gọi sau khi frontend nhận được access_token từ Google OAuth.
    /// Backend đồng bộ user vào local DB và trả về thông tin profile.
    /// </summary>
    [HttpPost("social/login")]
    [Authorize]
    public async Task<IActionResult> SocialLogin()
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token"));

        var result = await _authService.HandleSocialLoginAsync(userId);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Social login successful"));
    }
}
