using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.BackgroundJobs;

public class ExpireBoostJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<ExpireBoostJob> _logger;

    public ExpireBoostJob(AppDbContext db, ILogger<ExpireBoostJob> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var now = DateTimeOffset.UtcNow;

        var tourCount = await _db.Tours
            .Where(t => t.IsBoosted && t.BoostExpiresAt < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IsBoosted, false)
                .SetProperty(t => t.BoostExpiresAt, (DateTimeOffset?)null));

        var boostCount = await _db.Boosts
            .Where(b => b.Status == BoostStatus.active && b.ExpiresAt < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, BoostStatus.expired));

        if (tourCount > 0 || boostCount > 0)
            _logger.LogInformation(
                "ExpireBoostJob: de-boosted {TourCount} tour(s), marked {BoostCount} boost record(s) expired",
                tourCount, boostCount);
    }
}
