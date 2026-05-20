using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.BackgroundJobs;

public class SubscriptionExpiringWarningJob
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<SubscriptionExpiringWarningJob> _logger;

    public SubscriptionExpiringWarningJob(
        AppDbContext db,
        INotificationService notifications,
        ILogger<SubscriptionExpiringWarningJob> logger)
    {
        _db            = db;
        _notifications = notifications;
        _logger        = logger;
    }

    public async Task ExecuteAsync()
    {
        var now         = DateTimeOffset.UtcNow;
        var windowStart = now.AddDays(2);
        var windowEnd   = now.AddDays(4);

        var subs = await _db.Subscriptions
            .Where(s => s.Status == BoostStatus.active
                     && s.ExpiresAt >= windowStart
                     && s.ExpiresAt <= windowEnd)
            .ToListAsync();

        var count = 0;
        foreach (var sub in subs)
        {
            var alreadySent = await _db.Notifications
                .AnyAsync(n => n.EntityId == sub.Id && n.Type == "subscription_expiring");
            if (alreadySent) continue;

            var planName      = sub.Plan == SubscriptionPlan.pro ? "Pro" : "Premium";
            var expiresDisplay = sub.ExpiresAt.ToLocalTime().ToString("dd/MM/yyyy");
            var commissionAfter = "15%";

            await _notifications.CreateAsync(
                sub.GuideId,
                "subscription_expiring",
                "Gói đăng ký sắp hết hạn",
                $"Gói {planName} của bạn sẽ hết hạn vào {expiresDisplay}. Hãy gia hạn để tiếp tục ưu đãi commission.",
                "subscription", sub.Id,
                "Gói đăng ký sắp hết hạn - GuideMarket",
                $"""
                <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:24px;border:1px solid #e0e0e0;border-radius:8px">
                  <h2 style="color:#1565c0">📅 Gói {planName} sắp hết hạn</h2>
                  <p>Gói <strong>{planName}</strong> của bạn sẽ hết hạn vào <strong>{expiresDisplay}</strong>.</p>
                  <p>Sau khi hết hạn, tài khoản sẽ trở về gói <strong>Free</strong> với commission <strong>{commissionAfter}</strong>.</p>
                  <p style="margin-top:16px">Gia hạn ngay để tiếp tục hưởng ưu đãi commission thấp hơn và không giới hạn số tour!</p>
                </div>
                """);
            count++;
        }

        if (count > 0)
            _logger.LogInformation("SubscriptionExpiringWarningJob: sent {Count} expiry warning(s)", count);
    }
}
