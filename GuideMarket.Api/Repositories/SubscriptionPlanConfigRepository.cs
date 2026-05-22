using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class SubscriptionPlanConfigRepository : ISubscriptionPlanConfigRepository
{
    private readonly AppDbContext _db;

    public SubscriptionPlanConfigRepository(AppDbContext db) => _db = db;

    public Task<List<SubscriptionPlanConfig>> GetAllActiveAsync() =>
        _db.SubscriptionPlanConfigs
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync();

    public Task<SubscriptionPlanConfig?> GetByPlanAsync(string plan) =>
        _db.SubscriptionPlanConfigs.FirstOrDefaultAsync(p => p.Plan == plan);

    public void Update(SubscriptionPlanConfig config) =>
        _db.SubscriptionPlanConfigs.Update(config);
}
