using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class TourRepository : ITourRepository
{
    private readonly AppDbContext _db;

    public TourRepository(AppDbContext db) => _db = db;

    public async Task<Tour?> GetByIdAsync(Guid id) =>
        await _db.Tours.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id && t.DeletedAt == null);

    public async Task<Tour?> GetByIdWithGuideAsync(Guid id) =>
        await _db.Tours.AsNoTracking()
            .Include(t => t.Guide)
            .FirstOrDefaultAsync(t => t.Id == id && t.DeletedAt == null);

    public async Task<Tour?> FirstOrDefaultAsync(Expression<Func<Tour, bool>> predicate) =>
        await _db.Tours.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task<Tour?> GetBySlugAsync(string slug) =>
        await _db.Tours.AsNoTracking()
            .Include(t => t.Guide)
            .FirstOrDefaultAsync(t => t.Slug == slug && t.DeletedAt == null);

    public async Task<bool> SlugExistsAsync(string slug) =>
        await _db.Tours.AnyAsync(t => t.Slug == slug);

    public async Task<(List<Tour> Items, long Total)> SearchAsync(TourSearchParams p, bool publicOnly)
    {
        var query = _db.Tours.AsNoTracking()
            .Include(t => t.Guide)
            .Where(t => t.DeletedAt == null);

        if (publicOnly)
            query = query.Where(t => t.Status == TourStatus.active);

        if (!string.IsNullOrWhiteSpace(p.Q))
            query = query.Where(t => t.Title.Contains(p.Q) || t.Description.Contains(p.Q));

        if (!string.IsNullOrWhiteSpace(p.City))
            query = query.Where(t => EF.Functions.ILike(t.LocationCity, $"%{p.City}%"));

        if (!string.IsNullOrWhiteSpace(p.Category) && Enum.TryParse<TourCategory>(p.Category, true, out var cat))
            query = query.Where(t => t.Category == cat);

        if (!string.IsNullOrWhiteSpace(p.TourType) && Enum.TryParse<TourType>(p.TourType, true, out var tt))
            query = query.Where(t => t.TourType == tt);

        if (p.MinPrice.HasValue) query = query.Where(t => t.PricePerPerson >= p.MinPrice.Value);
        if (p.MaxPrice.HasValue) query = query.Where(t => t.PricePerPerson <= p.MaxPrice.Value);
        if (p.MinRating.HasValue) query = query.Where(t => t.AvgRating >= p.MinRating.Value);
        if (p.MinDuration.HasValue) query = query.Where(t => t.DurationHours >= p.MinDuration.Value);
        if (p.MaxDuration.HasValue) query = query.Where(t => t.DurationHours <= p.MaxDuration.Value);

        var total = await query.LongCountAsync();

        // Boosted tours first within each sort group
        query = p.Sort?.ToLower() switch
        {
            "price_asc"   => query.OrderByDescending(t => t.IsBoosted).ThenBy(t => t.PricePerPerson),
            "price_desc"  => query.OrderByDescending(t => t.IsBoosted).ThenByDescending(t => t.PricePerPerson),
            "rating_desc" => query.OrderByDescending(t => t.IsBoosted).ThenByDescending(t => t.AvgRating),
            _             => query.OrderByDescending(t => t.IsBoosted).ThenByDescending(t => t.CreatedAt),
        };

        var size = Math.Clamp(p.Size, 1, 100);
        var skip = (Math.Max(p.Page, 1) - 1) * size;

        var items = await query.Skip(skip).Take(size).ToListAsync();
        return (items, total);
    }

    public async Task<(List<Tour> Items, long Total)> GetByGuideIdAsync(Guid guideId, int page, int size)
    {
        var query = _db.Tours.AsNoTracking()
            .Include(t => t.Guide)
            .Where(t => t.GuideId == guideId && t.DeletedAt == null)
            .OrderByDescending(t => t.CreatedAt);

        var total = await query.LongCountAsync();
        var clampedSize = Math.Clamp(size, 1, 100);
        var skip = (Math.Max(page, 1) - 1) * clampedSize;
        var items = await query.Skip(skip).Take(clampedSize).ToListAsync();
        return (items, total);
    }

    public Task<int> CountActiveByGuideAsync(Guid guideId) =>
        _db.Tours.CountAsync(t => t.GuideId == guideId
            && t.Status == TourStatus.active && t.DeletedAt == null);

    public async Task AddAsync(Tour entity) => await _db.Tours.AddAsync(entity);

    public void Update(Tour entity) => _db.Tours.Update(entity);
    public void Delete(Tour entity) => _db.Tours.Remove(entity);

    // Availabilities
    public async Task<List<TourAvailability>> GetAvailabilitiesAsync(Guid tourId, bool upcomingOnly = false)
    {
        var query = _db.TourAvailabilities.AsNoTracking()
            .Where(a => a.TourId == tourId);

        if (upcomingOnly)
            query = query.Where(a => a.AvailableDate >= DateOnly.FromDateTime(DateTime.UtcNow) && !a.IsBlocked);

        return await query.OrderBy(a => a.AvailableDate).ToListAsync();
    }

    public async Task<TourAvailability?> GetAvailabilityByDateAsync(Guid tourId, DateOnly date) =>
        await _db.TourAvailabilities.AsNoTracking()
            .FirstOrDefaultAsync(a => a.TourId == tourId && a.AvailableDate == date);

    public async Task<List<TourAvailability>> GetAvailabilitiesByDateRangeAsync(
        Guid tourId, DateOnly startDate, DateOnly endDate) =>
        await _db.TourAvailabilities
            .Where(a => a.TourId == tourId
                     && a.AvailableDate >= startDate
                     && a.AvailableDate <= endDate)
            .ToListAsync();

    public async Task<List<TourAvailability>> GetAvailabilitiesBlockedByBookingAsync(Guid bookingId) =>
        await _db.TourAvailabilities
            .Where(a => a.BlockedByBookingId == bookingId)
            .ToListAsync();

    public async Task AddAvailabilityAsync(TourAvailability availability) =>
        await _db.TourAvailabilities.AddAsync(availability);

    public void UpdateAvailability(TourAvailability availability) =>
        _db.TourAvailabilities.Update(availability);

    public void DeleteAvailability(TourAvailability availability) =>
        _db.TourAvailabilities.Remove(availability);
}
