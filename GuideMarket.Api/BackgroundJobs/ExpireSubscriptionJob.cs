using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.BackgroundJobs;

public class ExpireSubscriptionJob
{
    private readonly AppDbContext _db;
    private readonly ILogger<ExpireSubscriptionJob> _logger;

    public ExpireSubscriptionJob(AppDbContext db, ILogger<ExpireSubscriptionJob> logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var now = DateTimeOffset.UtcNow;

        var expiredProfiles = await _db.GuideProfiles
            .Where(g => g.SubscriptionPlan != SubscriptionPlan.free
                     && g.SubscriptionExpiresAt < now)
            .ToListAsync();

        var expiredSubs = await _db.Subscriptions
            .Where(s => s.Status == BoostStatus.active && s.ExpiresAt < now)
            .ToListAsync();

        if (expiredProfiles.Count == 0 && expiredSubs.Count == 0) return;

        foreach (var profile in expiredProfiles)
        {
            profile.SubscriptionPlan      = SubscriptionPlan.free;
            profile.SubscriptionExpiresAt = null;
        }

        foreach (var sub in expiredSubs)
            sub.Status = BoostStatus.expired;

        if (expiredProfiles.Count > 0) _db.GuideProfiles.UpdateRange(expiredProfiles);
        if (expiredSubs.Count > 0)     _db.Subscriptions.UpdateRange(expiredSubs);

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "ExpireSubscriptionJob: downgraded {ProfileCount} guide(s), marked {SubCount} subscription(s) expired",
            expiredProfiles.Count, expiredSubs.Count);
    }
}
