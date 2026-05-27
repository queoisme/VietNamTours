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

        var profileCount = await _db.GuideProfiles
            .Where(g => g.SubscriptionPlan != SubscriptionPlan.free
                     && g.SubscriptionExpiresAt < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.SubscriptionPlan, SubscriptionPlan.free)
                .SetProperty(g => g.SubscriptionExpiresAt, (DateTimeOffset?)null));

        var subCount = await _db.Subscriptions
            .Where(s => s.Status == BoostStatus.active && s.ExpiresAt < now)
            .ExecuteUpdateAsync(s => s
                .SetProperty(s => s.Status, BoostStatus.expired));

        if (profileCount > 0 || subCount > 0)
            _logger.LogInformation(
                "ExpireSubscriptionJob: downgraded {ProfileCount} guide(s), marked {SubCount} subscription(s) expired",
                profileCount, subCount);
    }
}
