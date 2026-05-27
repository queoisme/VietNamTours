using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class BoostRepository : IBoostRepository
{
    private readonly AppDbContext _db;

    public BoostRepository(AppDbContext db) => _db = db;

    public Task<Boost?> GetByIdAsync(Guid id) =>
        _db.Boosts.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);

    public Task<Boost?> FirstOrDefaultAsync(Expression<Func<Boost, bool>> predicate) =>
        _db.Boosts.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(Boost entity) => await _db.Boosts.AddAsync(entity);
    public void Update(Boost entity) => _db.Boosts.Update(entity);
    public void Delete(Boost entity) => _db.Boosts.Remove(entity);

    public Task<Boost?> GetByPaymentTxnIdAsync(string txnId) =>
        _db.Boosts.AsNoTracking().FirstOrDefaultAsync(b => b.PaymentTxnId == txnId);

    public async Task<(List<Boost> Items, long Total)> GetByGuideIdAsync(Guid guideId, int page, int size)
    {
        var q = _db.Boosts.AsNoTracking()
            .Where(b => b.GuideId == guideId)
            .Include(b => b.Tour)
            .OrderByDescending(b => b.StartsAt);

        var total = await q.LongCountAsync();
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }
}
