using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class HomeCategoryDisplayRepository : IHomeCategoryDisplayRepository
{
    private readonly AppDbContext _db;

    public HomeCategoryDisplayRepository(AppDbContext db) => _db = db;

    public Task<List<HomeCategoryDisplay>> GetAllVisibleAsync() =>
        _db.HomeCategoryDisplays
           .AsNoTracking()
           .Where(c => c.IsVisible)
           .OrderBy(c => c.SortOrder)
           .ToListAsync();

    public Task<List<HomeCategoryDisplay>> GetAllAsync() =>
        _db.HomeCategoryDisplays
           .AsNoTracking()
           .OrderBy(c => c.SortOrder)
           .ToListAsync();

    public Task<HomeCategoryDisplay?> GetByIdAsync(int id) =>
        _db.HomeCategoryDisplays.FirstOrDefaultAsync(c => c.Id == id);

    public async Task AddAsync(HomeCategoryDisplay entity) =>
        await _db.HomeCategoryDisplays.AddAsync(entity);

    public void Update(HomeCategoryDisplay entity) =>
        _db.HomeCategoryDisplays.Update(entity);

    public void Remove(HomeCategoryDisplay entity) =>
        _db.HomeCategoryDisplays.Remove(entity);
}
