using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class BookingRepository : IBookingRepository
{
    private readonly AppDbContext _db;

    public BookingRepository(AppDbContext db) => _db = db;

    public async Task<Booking?> GetByIdAsync(Guid id) =>
        await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);

    public async Task<Booking?> GetByIdWithDetailsAsync(Guid id) =>
        await _db.Bookings.AsNoTracking()
            .Include(b => b.Tour)
            .Include(b => b.Customer)
            .Include(b => b.Guide)
            .Include(b => b.Conversation)
            .FirstOrDefaultAsync(b => b.Id == id);

    public async Task<Booking?> FirstOrDefaultAsync(Expression<Func<Booking, bool>> predicate) =>
        await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task<(List<Booking> Items, long Total)> GetByCustomerIdAsync(
        Guid customerId, string? status, int page, int size)
    {
        var query = _db.Bookings.AsNoTracking()
            .Include(b => b.Tour)
            .Where(b => b.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var s))
            query = query.Where(b => b.Status == s);

        var total = await query.LongCountAsync();
        var clampedSize = Math.Clamp(size, 1, 100);
        var skip = (Math.Max(page, 1) - 1) * clampedSize;
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip(skip).Take(clampedSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task<(List<Booking> Items, long Total)> GetByGuideIdAsync(
        Guid guideId, string? status, int page, int size)
    {
        var query = _db.Bookings.AsNoTracking()
            .Include(b => b.Tour)
            .Include(b => b.Customer)
            .Where(b => b.GuideId == guideId);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<BookingStatus>(status, ignoreCase: true, out var s))
            query = query.Where(b => b.Status == s);

        var total = await query.LongCountAsync();
        var clampedSize = Math.Clamp(size, 1, 100);
        var skip = (Math.Max(page, 1) - 1) * clampedSize;
        var items = await query
            .OrderByDescending(b => b.CreatedAt)
            .Skip(skip).Take(clampedSize)
            .ToListAsync();
        return (items, total);
    }

    public async Task<Booking?> GetByPaymentTxnIdAsync(string txnId) =>
        await _db.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.PaymentTxnId == txnId);

    public async Task AddAsync(Booking entity) => await _db.Bookings.AddAsync(entity);

    public void Update(Booking entity) => _db.Bookings.Update(entity);
    public void Delete(Booking entity) => _db.Bookings.Remove(entity);

    public async Task AddConversationAsync(Conversation conversation) =>
        await _db.Conversations.AddAsync(conversation);

    public async Task<int> CountActiveForTourDateAsync(
        Guid tourId, DateOnly tourDate, Guid? excludeBookingId = null)
    {
        var query = _db.Bookings
            .Where(b => b.TourId == tourId
                     && b.TourDate == tourDate
                     && b.Status != BookingStatus.cancelled
                     && b.Status != BookingStatus.rejected);

        if (excludeBookingId.HasValue)
            query = query.Where(b => b.Id != excludeBookingId.Value);

        return await query.CountAsync();
    }
}
