using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class OtpService : IOtpService
{
    private const int ExpiryMinutes = 10;
    private const int MaxAttempts = 5;

    private readonly IOtpRepository _repo;
    private readonly IEmailService _email;
    private readonly ILogger<OtpService> _logger;

    public OtpService(IOtpRepository repo, IEmailService email, ILogger<OtpService> logger)
    {
        _repo   = repo;
        _email  = email;
        _logger = logger;
    }

    public async Task GenerateAndSendAsync(string target, string type, string? ipAddress = null)
    {
        var code = GenerateCode();
        var otp = new OtpVerification
        {
            Target    = target.ToLowerInvariant(),
            Type      = type,
            Code      = code,
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(ExpiryMinutes),
            IpAddress = ipAddress,
        };

        await _repo.CreateAsync(otp);
        await SendEmailAsync(target, code, type);

        _logger.LogInformation("OTP generated for {Target} type={Type}", target, type);
    }

    public async Task<(OtpValidationResult Result, Guid OtpId)> ValidateAsync(
        string target, string code, string type)
    {
        var otp = await _repo.GetActiveAsync(target.ToLowerInvariant(), type);

        if (otp is null)
            return (OtpValidationResult.NotFound, Guid.Empty);

        if (otp.ExpiresAt <= DateTimeOffset.UtcNow)
            return (OtpValidationResult.Expired, otp.Id);

        if (otp.Attempts >= MaxAttempts)
        {
            // Vô hiệu hoá OTP khi vượt quá số lần thử
            await _repo.MarkUsedAsync(otp.Id);
            return (OtpValidationResult.MaxAttemptsExceeded, otp.Id);
        }

        if (otp.Code != code)
        {
            await _repo.IncrementAttemptsAsync(otp.Id);
            // Nếu đây là lần thử thứ MaxAttempts, vô hiệu hoá ngay
            if (otp.Attempts + 1 >= MaxAttempts)
                await _repo.MarkUsedAsync(otp.Id);
            return (OtpValidationResult.InvalidCode, otp.Id);
        }

        return (OtpValidationResult.Valid, otp.Id);
    }

    public Task MarkUsedAsync(Guid otpId) => _repo.MarkUsedAsync(otpId);

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------
    private static string GenerateCode()
    {
        var value = Random.Shared.Next(0, 1_000_000);
        return value.ToString("D6");
    }

    private Task SendEmailAsync(string email, string code, string type)
    {
        var (subject, body) = type switch
        {
            OtpTypes.EmailRegistration => BuildRegistrationEmail(code),
            OtpTypes.PasswordReset     => BuildPasswordResetEmail(code),
            _                          => BuildGenericEmail(code),
        };

        return _email.SendAsync(email, subject, body);
    }

    private static (string Subject, string Body) BuildRegistrationEmail(string code) => (
        "Mã xác thực đăng ký tài khoản VietNamTours",
        $"""
        <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:24px;border:1px solid #e0e0e0;border-radius:8px">
          <h2 style="color:#1a73e8">Xác thực tài khoản</h2>
          <p>Chào mừng bạn đến với <strong>VietNamTours</strong>!</p>
          <p>Mã xác thực đăng ký của bạn là:</p>
          <div style="text-align:center;margin:24px 0">
            <span style="font-size:36px;font-weight:bold;letter-spacing:8px;color:#1a73e8">{code}</span>
          </div>
          <p style="color:#666;font-size:14px">Mã có hiệu lực trong <strong>10 phút</strong>. Không chia sẻ mã này với bất kỳ ai.</p>
          <hr style="border:none;border-top:1px solid #e0e0e0;margin:24px 0">
          <p style="color:#999;font-size:12px">Nếu bạn không yêu cầu mã này, hãy bỏ qua email này.</p>
        </div>
        """
    );

    private static (string Subject, string Body) BuildPasswordResetEmail(string code) => (
        "Mã đặt lại mật khẩu VietNamTours",
        $"""
        <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:24px;border:1px solid #e0e0e0;border-radius:8px">
          <h2 style="color:#e53935">Đặt lại mật khẩu</h2>
          <p>Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn.</p>
          <p>Mã xác thực của bạn là:</p>
          <div style="text-align:center;margin:24px 0">
            <span style="font-size:36px;font-weight:bold;letter-spacing:8px;color:#e53935">{code}</span>
          </div>
          <p style="color:#666;font-size:14px">Mã có hiệu lực trong <strong>10 phút</strong>. Không chia sẻ mã này với bất kỳ ai.</p>
          <hr style="border:none;border-top:1px solid #e0e0e0;margin:24px 0">
          <p style="color:#999;font-size:12px">Nếu bạn không yêu cầu đặt lại mật khẩu, tài khoản của bạn vẫn an toàn — hãy bỏ qua email này.</p>
        </div>
        """
    );

    private static (string Subject, string Body) BuildGenericEmail(string code) => (
        "Mã xác thực VietNamTours",
        $"""
        <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:24px">
          <p>Mã xác thực của bạn là: <strong style="font-size:24px;letter-spacing:4px">{code}</strong></p>
          <p style="color:#666;font-size:14px">Mã có hiệu lực trong 10 phút.</p>
        </div>
        """
    );
}

public static class OtpTypes
{
    public const string EmailRegistration = "email_registration";
    public const string PasswordReset     = "password_reset";
    public const string PhoneVerification = "phone_verification";
}
