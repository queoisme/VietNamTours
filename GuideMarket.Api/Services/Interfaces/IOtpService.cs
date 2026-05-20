namespace GuideMarket.Api.Services.Interfaces;

public enum OtpValidationResult
{
    Valid,
    InvalidCode,
    Expired,
    MaxAttemptsExceeded,
    NotFound,
}

public interface IOtpService
{
    Task GenerateAndSendAsync(string target, string type, string? ipAddress = null);
    Task<(OtpValidationResult Result, Guid OtpId)> ValidateAsync(string target, string code, string type);
    Task MarkUsedAsync(Guid otpId);
}
