using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class BoostPlanConfigRepository : IBoostPlanConfigRepository
{
    private readonly AppDbContext _db;

    public BoostPlanConfigRepository(AppDbContext db) => _db = db;

    public Task<List<BoostPlanConfig>> GetAllActiveAsync() =>
        _db.BoostPlanConfigs
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync();

    public Task<BoostPlanConfig?> GetByPlanAsync(string plan) =>
        _db.BoostPlanConfigs.FirstOrDefaultAsync(p => p.Plan == plan);

    public void Update(BoostPlanConfig config) =>
        _db.BoostPlanConfigs.Update(config);
}
