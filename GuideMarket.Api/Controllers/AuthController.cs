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

    /// <summary>Đăng ký tài khoản mới. Supabase sẽ gửi email xác nhận.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        return StatusCode(201, ApiResponse<RegisterResponse>.Ok(result,
            "Registration successful. Please check your email to confirm your account."));
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

    /// <summary>Đặt lại mật khẩu mới (dùng token từ email reset).</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        await _authService.ResetPasswordAsync(request.AccessToken, request.NewPassword);
        return Ok(ApiResponse<object>.Ok(null!, "Password reset successfully"));
    }

    /// <summary>Xác nhận email bằng OTP 6 số. Trả về token luôn.</summary>
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var result = await _authService.VerifyEmailAsync(request.Email, request.Otp);
        return Ok(ApiResponse<LoginResponse>.Ok(result, "Email verified successfully"));
    }

    /// <summary>Supabase email confirmation callback (Implicit flow). Trả HTML thành công.</summary>
    [HttpGet("/auth/callback")]
    public IActionResult Callback() => Content("""
        <!DOCTYPE html>
        <html lang="vi">
        <head>
          <meta charset="UTF-8">
          <title>Email Confirmed</title>
          <style>
            body { font-family: sans-serif; display:flex; justify-content:center;
                   align-items:center; height:100vh; margin:0; background:#f0fdf4; }
            .card { background:#fff; border-radius:12px; padding:40px; text-align:center;
                    box-shadow:0 4px 24px rgba(0,0,0,.1); max-width:400px; }
            h2 { color:#16a34a; margin-bottom:8px; }
            p  { color:#555; }
            a  { color:#2563eb; }
          </style>
          <script>
            // Với Implicit flow, tokens nằm trong URL fragment (#access_token=...)
            window.onload = function() {
              var hash = window.location.hash;
              if (hash.includes('access_token')) {
                document.getElementById('msg').textContent =
                  'Email xác nhận thành công! Bạn đã có thể đăng nhập.';
              } else if (hash.includes('error')) {
                document.getElementById('msg').textContent = 'Lỗi xác nhận: ' + hash;
                document.getElementById('title').textContent = 'Lỗi';
                document.getElementById('title').style.color = '#dc2626';
              }
            };
          </script>
        </head>
        <body>
          <div class="card">
            <h2 id="title">✓ Email đã xác nhận</h2>
            <p id="msg">Email xác nhận thành công! Bạn đã có thể đăng nhập.</p>
            <p style="margin-top:24px">
              <a href="/swagger">Mở Swagger để test login →</a>
            </p>
          </div>
        </body>
        </html>
        """, "text/html");

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
}
