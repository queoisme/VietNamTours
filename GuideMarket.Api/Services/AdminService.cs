using System.Text;
using GuideMarket.Api.Data;
using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Services;

public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly IUnitOfWork _uow;

    public AdminService(AppDbContext db, IUnitOfWork uow)
    {
        _db  = db;
        _uow = uow;
    }

    public async Task<AdminStatsResponse> GetStatsAsync(Guid adminId)
    {
        await RequireAdminAsync(adminId);

        var totalUsers      = await _db.Users.CountAsync(u => u.DeletedAt == null);
        var totalGuides     = await _db.Users.CountAsync(u => u.Role == UserRole.guide && u.DeletedAt == null);
        var totalCustomers  = await _db.Users.CountAsync(u => u.Role == UserRole.customer && u.DeletedAt == null);
        var totalTours      = await _db.Tours.CountAsync(t => t.DeletedAt == null);
        var activeTours     = await _db.Tours.CountAsync(t => t.Status == TourStatus.active && t.DeletedAt == null);
        var totalBookings   = await _db.Bookings.CountAsync();
        var pendingBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.pending);
        var confirmedBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.confirmed);
        var completedBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.completed);
        var cancelledBookings = await _db.Bookings.CountAsync(b => b.Status == BookingStatus.cancelled);
        var totalRevenue    = await _db.Bookings
            .Where(b => b.Status == BookingStatus.completed)
            .SumAsync(b => (decimal?)b.TotalPrice) ?? 0;

        return new AdminStatsResponse
        {
            TotalUsers        = totalUsers,
            TotalGuides       = totalGuides,
            TotalCustomers    = totalCustomers,
            TotalTours        = totalTours,
            ActiveTours       = activeTours,
            TotalBookings     = totalBookings,
            PendingBookings   = pendingBookings,
            ConfirmedBookings = confirmedBookings,
            CompletedBookings = completedBookings,
            CancelledBookings = cancelledBookings,
            TotalRevenue      = totalRevenue,
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

        var query = _db.Bookings.AsNoTracking()
            .Include(b => b.Tour)
            .Include(b => b.Customer)
            .Include(b => b.Guide)
            .AsQueryable();

        if (from.HasValue)   query = query.Where(b => b.TourDate >= from.Value);
        if (to.HasValue)     query = query.Where(b => b.TourDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var bs))
            query = query.Where(b => b.Status == bs);

        var bookings = await query.OrderByDescending(b => b.CreatedAt).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Id,TourTitle,CustomerName,GuideName,TourDate,NumPeople,TotalPrice,Status,PaymentStatus,CreatedAt");
        foreach (var b in bookings)
        {
            sb.AppendLine(string.Join(",",
                b.Id,
                Csv(b.Tour.Title),
                Csv(b.Customer.FullName),
                Csv(b.Guide.FullName),
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
}
