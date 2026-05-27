using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.BackgroundJobs;

public class BoostExpiringWarningJob
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<BoostExpiringWarningJob> _logger;

    public BoostExpiringWarningJob(
        AppDbContext db,
        INotificationService notifications,
        ILogger<BoostExpiringWarningJob> logger)
    {
        _db            = db;
        _notifications = notifications;
        _logger        = logger;
    }

    public async Task ExecuteAsync()
    {
        var now         = DateTimeOffset.UtcNow;
        var windowStart = now.AddHours(23);
        var windowEnd   = now.AddHours(25);

        var boosts = await _db.Boosts
            .Include(b => b.Tour)
            .Where(b => b.Status == BoostStatus.active
                     && b.ExpiresAt >= windowStart
                     && b.ExpiresAt <= windowEnd)
            .ToListAsync();

        if (boosts.Count == 0) return;

        // Batch-fetch all already-sent IDs to avoid N+1 per boost
        var boostIds = boosts.Select(b => b.Id).ToList();
        var alreadySentIds = (await _db.Notifications
            .Where(n => boostIds.Contains(n.EntityId!.Value) && n.Type == "boost_expiring")
            .Select(n => n.EntityId!.Value)
            .ToListAsync()).ToHashSet();

        var count = 0;
        foreach (var boost in boosts)
        {
            if (alreadySentIds.Contains(boost.Id)) continue;

            var title = boost.Tour?.Title ?? "Tour";
            var expiresDisplay = boost.ExpiresAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");

            await _notifications.CreateAsync(
                boost.GuideId,
                "boost_expiring",
                "Tour sắp hết boost",
                $"Tour \"{title}\" sẽ hết boost sau khoảng 24 giờ nữa ({expiresDisplay}).",
                "boost", boost.Id,
                "Tour sắp hết boost - VietNamTours",
                $"""
                <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:24px;border:1px solid #e0e0e0;border-radius:8px">
                  <h2 style="color:#e65100">⏰ Tour sắp hết boost</h2>
                  <p>Tour <strong>{title}</strong> của bạn sẽ hết hiệu lực boost vào <strong>{expiresDisplay}</strong>.</p>
                  <p>Sau khi hết boost, tour sẽ không còn hiển thị ưu tiên trên trang chủ.</p>
                  <p style="margin-top:16px">Hãy gia hạn boost để tiếp tục thu hút khách hàng!</p>
                </div>
                """);
            count++;
        }

        if (count > 0)
            _logger.LogInformation("BoostExpiringWarningJob: sent {Count} expiry warning(s)", count);
    }
}
