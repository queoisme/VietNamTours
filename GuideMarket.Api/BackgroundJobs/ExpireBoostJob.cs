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

        var expiredTours = await _db.Tours
            .Where(t => t.IsBoosted && t.BoostExpiresAt < now)
            .ToListAsync();

        var expiredBoosts = await _db.Boosts
            .Where(b => b.Status == BoostStatus.active && b.ExpiresAt < now)
            .ToListAsync();

        if (expiredTours.Count == 0 && expiredBoosts.Count == 0) return;

        foreach (var tour in expiredTours)
        {
            tour.IsBoosted      = false;
            tour.BoostExpiresAt = null;
        }

        foreach (var boost in expiredBoosts)
            boost.Status = BoostStatus.expired;

        if (expiredTours.Count > 0)  _db.Tours.UpdateRange(expiredTours);
        if (expiredBoosts.Count > 0) _db.Boosts.UpdateRange(expiredBoosts);

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "ExpireBoostJob: de-boosted {TourCount} tour(s), marked {BoostCount} boost record(s) expired",
            expiredTours.Count, expiredBoosts.Count);
    }
}
