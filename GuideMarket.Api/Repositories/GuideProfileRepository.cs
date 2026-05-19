using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class GuideProfileRepository : IGuideProfileRepository
{
    private readonly AppDbContext _db;

    public GuideProfileRepository(AppDbContext db) => _db = db;

    public async Task<GuideProfile?> GetByIdAsync(Guid id) =>
        await _db.GuideProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);

    public async Task<GuideProfile?> GetByUserIdAsync(Guid userId) =>
        await _db.GuideProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task<GuideProfile?> GetByUserIdWithUserAsync(Guid userId) =>
        await _db.GuideProfiles.AsNoTracking()
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task<bool> ExistsByUserIdAsync(Guid userId) =>
        await _db.GuideProfiles.AnyAsync(p => p.UserId == userId);

    public async Task<GuideProfile?> FirstOrDefaultAsync(Expression<Func<GuideProfile, bool>> predicate) =>
        await _db.GuideProfiles.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(GuideProfile entity) => await _db.GuideProfiles.AddAsync(entity);

    public void Update(GuideProfile entity) => _db.GuideProfiles.Update(entity);
    public void Delete(GuideProfile entity) => _db.GuideProfiles.Remove(entity);
}
