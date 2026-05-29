using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.BackgroundJobs;

public class ExpireUnpaidBookingJob
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly ILogger<ExpireUnpaidBookingJob> _logger;

    public ExpireUnpaidBookingJob(
        AppDbContext db,
        INotificationService notifications,
        ILogger<ExpireUnpaidBookingJob> logger)
    {
        _db            = db;
        _notifications = notifications;
        _logger        = logger;
    }

    public async Task ExecuteAsync()
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-30);

        var expired = await _db.Bookings
            .Where(b => b.Status == BookingStatus.pending
                     && b.PaymentStatus == PaymentStatus.unpaid
                     && b.CreatedAt < cutoff)
            .Select(b => new
            {
                b.Id,
                b.CustomerId,
                b.TourId,
                b.TourDate,
                b.NumPeople,
                TourType  = b.Tour.TourType,
                TourTitle = b.Tour.Title,
            })
            .ToListAsync();

        if (expired.Count == 0) return;

        var ids = expired.Select(b => b.Id).ToList();

        await _db.Bookings
            .Where(b => ids.Contains(b.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, BookingStatus.cancelled)
                .SetProperty(b => b.CancellationBy, CancellationBy.system)
                .SetProperty(b => b.CancellationReason, "Hủy tự động: chưa thanh toán trong 30 phút"));

        var privateBookings = expired.Where(b => b.TourType == TourType.@private).ToList();

        // Unblock consecutive dates for private tours
        if (privateBookings.Count > 0)
        {
            var privateIds = privateBookings.Select(b => b.Id).ToList();

            await _db.TourAvailabilities
                .Where(a => a.BlockedByBookingId.HasValue
                         && privateIds.Contains(a.BlockedByBookingId.Value))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.IsBlocked, false)
                    .SetProperty(a => a.BlockedByBookingId, (Guid?)null));
        }

        // Release booked_slots for ALL expired bookings (private: -1, group: -numPeople)
        foreach (var b in expired)
        {
            var decrement = b.TourType == TourType.@private ? (short)1 : b.NumPeople;
            await _db.TourAvailabilities
                .Where(a => a.TourId == b.TourId && a.AvailableDate == b.TourDate)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(a => a.BookedSlots,
                        a => a.BookedSlots >= decrement
                            ? (short)(a.BookedSlots - decrement)
                            : (short)0));
        }

        foreach (var b in expired)
        {
            await _notifications.CreateAsync(
                b.CustomerId,
                "booking_cancelled",
                "Đơn đặt tour đã bị hủy",
                $"Đơn đặt tour \"{b.TourTitle}\" đã bị hủy tự động do chưa thanh toán trong 30 phút.",
                "booking", b.Id,
                "Đơn đặt tour bị hủy tự động - VietNamTours",
                $"""
                <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:24px;border:1px solid #e0e0e0;border-radius:8px">
                  <h2 style="color:#c62828">Đơn đặt tour bị hủy tự động</h2>
                  <p>Đơn đặt tour <strong>{b.TourTitle}</strong> của bạn đã bị hủy do <strong>chưa thanh toán trong 30 phút</strong>.</p>
                  <p>Nếu bạn vẫn muốn đặt tour này, vui lòng tạo đơn mới và hoàn tất thanh toán ngay.</p>
                </div>
                """);
        }

        _logger.LogInformation(
            "ExpireUnpaidBookingJob: cancelled {Count} unpaid booking(s)", expired.Count);
    }
}
