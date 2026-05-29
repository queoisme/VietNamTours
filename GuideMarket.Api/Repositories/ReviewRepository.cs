using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class ReviewRepository : IReviewRepository
{
    private readonly AppDbContext _db;

    public ReviewRepository(AppDbContext db) => _db = db;

    public Task<Review?> GetByIdAsync(Guid id) =>
        _db.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);

    public Task<Review?> FirstOrDefaultAsync(Expression<Func<Review, bool>> predicate) =>
        _db.Reviews.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(Review entity) => await _db.Reviews.AddAsync(entity);
    public void Update(Review entity) => _db.Reviews.Update(entity);
    public void Delete(Review entity) => _db.Reviews.Remove(entity);

    public Task<Review?> GetByBookingIdAsync(Guid bookingId) =>
        _db.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.BookingId == bookingId);

    public Task<Review?> GetByIdWithDetailsAsync(Guid id) =>
        _db.Reviews.AsNoTracking()
            .Include(r => r.Customer)
            .Include(r => r.Tour)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<(List<Review> Items, long Total)> GetByTourIdAsync(Guid tourId, int page, int size)
    {
        var q = _db.Reviews.AsNoTracking()
            .Where(r => r.TourId == tourId && r.IsVisible)
            .Include(r => r.Customer)
            .OrderByDescending(r => r.CreatedAt);

        var total = await q.LongCountAsync();
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }

    public async Task<(List<Review> Items, long Total)> GetByCustomerIdAsync(Guid customerId, int page, int size)
    {
        var q = _db.Reviews.AsNoTracking()
            .Where(r => r.CustomerId == customerId)
            .Include(r => r.Tour)
            .OrderByDescending(r => r.CreatedAt);

        var total = await q.LongCountAsync();
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }

    public async Task<HashSet<Guid>> GetReviewedBookingIdsAsync(IEnumerable<Guid> bookingIds)
    {
        var ids = bookingIds.ToList();
        if (ids.Count == 0) return [];
        var reviewed = await _db.Reviews
            .Where(r => ids.Contains(r.BookingId))
            .Select(r => r.BookingId)
            .ToListAsync();
        return reviewed.ToHashSet();
    }
}
