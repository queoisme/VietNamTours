using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class WithdrawalRepository : IWithdrawalRepository
{
    private readonly AppDbContext _db;

    public WithdrawalRepository(AppDbContext db) => _db = db;

    public Task<Withdrawal?> GetByIdAsync(Guid id) =>
        _db.Withdrawals.AsNoTracking().FirstOrDefaultAsync(w => w.Id == id);

    public Task<Withdrawal?> FirstOrDefaultAsync(Expression<Func<Withdrawal, bool>> predicate) =>
        _db.Withdrawals.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(Withdrawal entity) => await _db.Withdrawals.AddAsync(entity);
    public void Update(Withdrawal entity) => _db.Withdrawals.Update(entity);
    public void Delete(Withdrawal entity) => _db.Withdrawals.Remove(entity);

    public Task<Withdrawal?> GetByIdWithGuideAsync(Guid id) =>
        _db.Withdrawals.Include(w => w.Guide).FirstOrDefaultAsync(w => w.Id == id);

    public async Task<(List<Withdrawal> Items, long Total)> GetByGuideIdAsync(Guid guideId, int page, int size)
    {
        var q = _db.Withdrawals.AsNoTracking()
            .Where(w => w.GuideId == guideId)
            .OrderByDescending(w => w.CreatedAt);

        var total = await q.LongCountAsync();
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }

    public async Task<(List<Withdrawal> Items, long Total)> GetAllAsync(string? status, int page, int size)
    {
        var q = _db.Withdrawals.AsNoTracking()
            .Include(w => w.Guide)
            .AsQueryable();

        if (Enum.TryParse<WithdrawalStatus>(status, true, out var s))
            q = q.Where(w => w.Status == s);

        q = q.OrderByDescending(w => w.CreatedAt);

        var total = await q.LongCountAsync();
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }
}
