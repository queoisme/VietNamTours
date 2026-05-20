using GuideMarket.Api.Repositories;

namespace GuideMarket.Api.BackgroundJobs;

public class CleanupExpiredOtpJob
{
    private readonly IOtpRepository _repo;
    private readonly ILogger<CleanupExpiredOtpJob> _logger;

    public CleanupExpiredOtpJob(IOtpRepository repo, ILogger<CleanupExpiredOtpJob> logger)
    {
        _repo   = repo;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        // Xóa OTP đã hết hạn hơn 1 ngày (giữ lại để debug nếu cần)
        var cutoff = DateTimeOffset.UtcNow.AddDays(-1);
        await _repo.DeleteExpiredAsync(cutoff);
        _logger.LogInformation("CleanupExpiredOtpJob: removed OTP records expired before {Cutoff}", cutoff);
    }
}
