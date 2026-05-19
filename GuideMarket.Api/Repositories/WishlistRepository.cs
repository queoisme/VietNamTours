using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class WishlistRepository : IWishlistRepository
{
    private readonly AppDbContext _db;

    public WishlistRepository(AppDbContext db) => _db = db;

    public Task<Wishlist?> GetByIdAsync(Guid id) =>
        _db.Wishlists.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);

    public Task<Wishlist?> FirstOrDefaultAsync(Expression<Func<Wishlist, bool>> predicate) =>
        _db.Wishlists.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(Wishlist entity) => await _db.Wishlists.AddAsync(entity);
    public void Update(Wishlist entity) => _db.Wishlists.Update(entity);
    public void Delete(Wishlist entity) => _db.Wishlists.Remove(entity);

    public Task<Wishlist?> GetByCustomerAndTourAsync(Guid customerId, Guid tourId) =>
        _db.Wishlists.AsNoTracking()
            .FirstOrDefaultAsync(w => w.CustomerId == customerId && w.TourId == tourId);

    public async Task<(List<Wishlist> Items, long Total)> GetByCustomerIdAsync(Guid customerId, int page, int size)
    {
        var q = _db.Wishlists.AsNoTracking()
            .Where(w => w.CustomerId == customerId)
            .Include(w => w.Tour)
            .OrderByDescending(w => w.CreatedAt);

        var total = await q.LongCountAsync();
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }
}
