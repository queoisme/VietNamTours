using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IOtpRepository
{
    Task<OtpVerification?> GetActiveAsync(string target, string type);
    Task CreateAsync(OtpVerification otp);
    Task MarkUsedAsync(Guid id);
    Task IncrementAttemptsAsync(Guid id);
    Task DeleteExpiredAsync(DateTimeOffset before);
}
