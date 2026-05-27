using System.Text;
using GuideMarket.Api.Data;
using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace GuideMarket.Api.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly IUnitOfWork _uow;
    private readonly IMemoryCache _cache;
    private const string StatsCacheKey = "admin_stats";

    public AdminService(AppDbContext db, IUnitOfWork uow, IMemoryCache cache)
    {
        _db    = db;
        _uow   = uow;
        _cache = cache;
    }

    public async Task<AdminStatsResponse> GetStatsAsync(Guid adminId)
    {
        await RequireAdminAsync(adminId);

        return await _cache.GetOrCreateAsync(StatsCacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(2);
            return await FetchStatsAsync();
        }) ?? await FetchStatsAsync();
    }

    private async Task<AdminStatsResponse> FetchStatsAsync()
    {
        // 3 queries with conditional aggregation instead of 11 separate round-trips
        var userStats = await _db.Database
            .SqlQuery<AdminUserStatsRaw>($"""
                SELECT
                  COUNT(*) FILTER (WHERE deleted_at IS NULL)                       AS total_users,
                  COUNT(*) FILTER (WHERE role='guide'    AND deleted_at IS NULL)    AS total_guides,
                  COUNT(*) FILTER (WHERE role='customer' AND deleted_at IS NULL)    AS total_customers
                FROM users
                """)
            .FirstAsync();

        var tourStats = await _db.Database
            .SqlQuery<AdminTourStatsRaw>($"""
                SELECT
                  COUNT(*) FILTER (WHERE deleted_at IS NULL)                       AS total_tours,
                  COUNT(*) FILTER (WHERE status='active' AND deleted_at IS NULL)   AS active_tours
                FROM tours
                """)
            .FirstAsync();

        var bookingStats = await _db.Database
            .SqlQuery<AdminBookingStatsRaw>($"""
                SELECT
                  COUNT(*)                                                               AS total_bookings,
                  COUNT(*) FILTER (WHERE status='pending')                              AS pending_bookings,
                  COUNT(*) FILTER (WHERE status='confirmed')                            AS confirmed_bookings,
                  COUNT(*) FILTER (WHERE status='completed')                            AS completed_bookings,
                  COUNT(*) FILTER (WHERE status='cancelled')                            AS cancelled_bookings,
                  COALESCE(SUM(total_price) FILTER (WHERE status='completed'), 0)       AS total_revenue
                FROM bookings
                """)
            .FirstAsync();

        return new AdminStatsResponse
        {
            TotalUsers        = (int)userStats.TotalUsers,
            TotalGuides       = (int)userStats.TotalGuides,
            TotalCustomers    = (int)userStats.TotalCustomers,
            TotalTours        = (int)tourStats.TotalTours,
            ActiveTours       = (int)tourStats.ActiveTours,
            TotalBookings     = (int)bookingStats.TotalBookings,
            PendingBookings   = (int)bookingStats.PendingBookings,
            ConfirmedBookings = (int)bookingStats.ConfirmedBookings,
            CompletedBookings = (int)bookingStats.CompletedBookings,
            CancelledBookings = (int)bookingStats.CancelledBookings,
            TotalRevenue      = bookingStats.TotalRevenue,
        };
    }

    public async Task<AdminRevenueResponse> GetRevenueAsync(Guid adminId, DateOnly? from, DateOnly? to)
    {
        await RequireAdminAsync(adminId);

        var query = _db.Bookings.Where(b => b.Status == BookingStatus.completed);
        if (from.HasValue) query = query.Where(b => b.TourDate >= from.Value);
        if (to.HasValue)   query = query.Where(b => b.TourDate <= to.Value);

        var total   = await query.SumAsync(b => (decimal?)b.TotalPrice) ?? 0;
        var count   = await query.CountAsync();
        var byDate  = await query
            .GroupBy(b => b.TourDate)
            .Select(g => new AdminRevenueItem
            {
                Date         = g.Key,
                BookingCount = g.Count(),
                Revenue      = g.Sum(b => b.TotalPrice),
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        return new AdminRevenueResponse { TotalRevenue = total, TotalBookings = count, ByDate = byDate };
    }

    public async Task<(List<AdminUserResponse> Items, long Total)> GetUsersAsync(
        Guid adminId, string? role, string? q, int page, int size)
    {
        await RequireAdminAsync(adminId);

        var query = _db.Users.AsNoTracking().Where(u => u.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<UserRole>(role, ignoreCase: true, out var r))
            query = query.Where(u => u.Role == r);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(u => u.FullName.Contains(q) || u.Email.Contains(q));

        var total       = await query.LongCountAsync();
        var clampedSize = Math.Clamp(size, 1, 100);
        var skip        = (Math.Max(page, 1) - 1) * clampedSize;
        var items       = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip(skip).Take(clampedSize)
            .Select(u => MapUser(u))
            .ToListAsync();

        return (items, total);
    }

    public async Task<AdminUserResponse> GetUserByIdAsync(Guid adminId, Guid userId)
    {
        await RequireAdminAsync(adminId);
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null)
            ?? throw new KeyNotFoundException("User not found");
        return MapUser(user);
    }

    public async Task BanUserAsync(Guid adminId, Guid userId, BanUserRequest request)
    {
        await RequireAdminAsync(adminId);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.DeletedAt == null)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role == UserRole.admin)
            throw new ForbiddenAccessException("Cannot ban an admin account");

        user.IsBanned  = request.IsBanned;
        user.BanReason = request.IsBanned ? request.Reason : null;
        _db.Users.Update(user);
        await _db.SaveChangesAsync();
    }

    public async Task<(List<AdminTourResponse> Items, long Total)> GetToursAsync(
        Guid adminId, string? status, int page, int size)
    {
        await RequireAdminAsync(adminId);

        var query = _db.Tours.AsNoTracking()
            .Include(t => t.Guide)
            .Where(t => t.DeletedAt == null);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TourStatus>(status, ignoreCase: true, out var s))
            query = query.Where(t => t.Status == s);

        var total       = await query.LongCountAsync();
        var clampedSize = Math.Clamp(size, 1, 100);
        var skip        = (Math.Max(page, 1) - 1) * clampedSize;
        var items       = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip(skip).Take(clampedSize)
            .ToListAsync();

        return (items.Select(MapTour).ToList(), total);
    }

    public async Task UpdateTourStatusAsync(Guid adminId, Guid tourId, UpdateTourStatusRequest request)
    {
        await RequireAdminAsync(adminId);
        var tour = await _db.Tours.FirstOrDefaultAsync(t => t.Id == tourId && t.DeletedAt == null)
            ?? throw new KeyNotFoundException("Tour not found");
        if (!Enum.TryParse<TourStatus>(request.Status, ignoreCase: true, out var status))
            throw new InvalidOperationException("Invalid tour status");

        tour.Status = status;
        _db.Tours.Update(tour);
        await _db.SaveChangesAsync();
    }

    public async Task<byte[]> ExportBookingsAsync(Guid adminId, DateOnly? from, DateOnly? to, string? status)
    {
        await RequireAdminAsync(adminId);

        if (!from.HasValue && !to.HasValue && string.IsNullOrWhiteSpace(status))
            throw new InvalidOperationException("Export requires at least one filter: 'from' date, 'to' date, or 'status'.");

        if (from.HasValue && to.HasValue && (to.Value.DayNumber - from.Value.DayNumber) > 366)
            throw new InvalidOperationException("Date range cannot exceed 366 days.");

        var query = _db.Bookings.AsNoTracking().AsQueryable();

        if (from.HasValue)   query = query.Where(b => b.TourDate >= from.Value);
        if (to.HasValue)     query = query.Where(b => b.TourDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var bs))
            query = query.Where(b => b.Status == bs);

        var bookings = await query
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id,
                TourTitle    = b.Tour.Title,
                CustomerName = b.Customer.FullName,
                GuideName    = b.Guide.FullName,
                b.TourDate,
                b.NumPeople,
                b.TotalPrice,
                b.Status,
                b.PaymentStatus,
                b.CreatedAt,
            })
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Id,TourTitle,CustomerName,GuideName,TourDate,NumPeople,TotalPrice,Status,PaymentStatus,CreatedAt");
        foreach (var b in bookings)
        {
            sb.AppendLine(string.Join(",",
                b.Id,
                Csv(b.TourTitle),
                Csv(b.CustomerName),
                Csv(b.GuideName),
                b.TourDate.ToString("yyyy-MM-dd"),
                b.NumPeople,
                b.TotalPrice,
                b.Status,
                b.PaymentStatus,
                b.CreatedAt.ToString("O")));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task RequireAdminAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.admin)
            throw new ForbiddenAccessException("Only admins can perform this action");
    }

    private static string Csv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;

    private static AdminUserResponse MapUser(User u) => new()
    {
        Id         = u.Id,
        Email      = u.Email,
        FullName   = u.FullName,
        Phone      = u.Phone,
        AvatarUrl  = u.AvatarUrl,
        Role       = u.Role.ToString(),
        IsVerified = u.IsVerified,
        IsBanned   = u.IsBanned,
        BanReason  = u.BanReason,
        CreatedAt  = u.CreatedAt,
    };

    private static AdminTourResponse MapTour(Tour t) => new()
    {
        Id             = t.Id,
        Title          = t.Title,
        LocationCity   = t.LocationCity,
        Category       = t.Category.ToString(),
        Status         = t.Status.ToString(),
        PricePerPerson = t.PricePerPerson,
        AvgRating      = t.AvgRating,
        TotalBookings  = t.TotalBookings,
        IsBoosted      = t.IsBoosted,
        GuideId        = t.GuideId,
        GuideName      = t.Guide.FullName,
        CreatedAt      = t.CreatedAt,
    };

    private record AdminUserStatsRaw(long TotalUsers, long TotalGuides, long TotalCustomers);
    private record AdminTourStatsRaw(long TotalTours, long ActiveTours);
    private record AdminBookingStatsRaw(
        long TotalBookings, long PendingBookings, long ConfirmedBookings,
        long CompletedBookings, long CancelledBookings, decimal TotalRevenue);
}
