using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly AppDbContext _db;

    public SubscriptionRepository(AppDbContext db) => _db = db;

    public Task<Subscription?> GetByIdAsync(Guid id) =>
        _db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);

    public Task<Subscription?> FirstOrDefaultAsync(Expression<Func<Subscription, bool>> predicate) =>
        _db.Subscriptions.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(Subscription entity) => await _db.Subscriptions.AddAsync(entity);
    public void Update(Subscription entity) => _db.Subscriptions.Update(entity);
    public void Delete(Subscription entity) => _db.Subscriptions.Remove(entity);

    public Task<Subscription?> GetByPaymentTxnIdAsync(string txnId) =>
        _db.Subscriptions.FirstOrDefaultAsync(s => s.PaymentTxnId == txnId);

    public Task<Subscription?> GetActiveByGuideIdAsync(Guid guideId) =>
        _db.Subscriptions.AsNoTracking()
            .Where(s => s.GuideId == guideId && s.Status == BoostStatus.active)
            .OrderByDescending(s => s.ExpiresAt)
            .FirstOrDefaultAsync();
}
