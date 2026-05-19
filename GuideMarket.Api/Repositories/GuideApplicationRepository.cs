using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class GuideApplicationRepository : IGuideApplicationRepository
{
    private readonly AppDbContext _db;

    public GuideApplicationRepository(AppDbContext db) => _db = db;

    public async Task<GuideApplication?> GetByIdAsync(Guid id) =>
        await _db.GuideApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);

    public async Task<GuideApplication?> GetByIdWithUsersAsync(Guid id) =>
        await _db.GuideApplications.AsNoTracking()
            .Include(a => a.Applicant)
            .Include(a => a.Reviewer)
            .FirstOrDefaultAsync(a => a.Id == id);

    public async Task<List<GuideApplication>> GetByUserIdAsync(Guid userId) =>
        await _db.GuideApplications.AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

    public async Task<bool> HasPendingOrApprovedAsync(Guid userId) =>
        await _db.GuideApplications.AnyAsync(a =>
            a.UserId == userId &&
            (a.Status == ApplicationStatus.pending || a.Status == ApplicationStatus.approved));

    public async Task<(List<GuideApplication> Items, long Total)> GetAllAsync(GuideApplicationListParams p)
    {
        var query = _db.GuideApplications.AsNoTracking()
            .Include(a => a.Applicant)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(p.Status) && Enum.TryParse<ApplicationStatus>(p.Status, true, out var status))
            query = query.Where(a => a.Status == status);

        var total = await query.LongCountAsync();

        var size = Math.Clamp(p.Size, 1, 100);
        var skip = (Math.Max(p.Page, 1) - 1) * size;

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(skip).Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task<GuideApplication?> FirstOrDefaultAsync(Expression<Func<GuideApplication, bool>> predicate) =>
        await _db.GuideApplications.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(GuideApplication entity) => await _db.GuideApplications.AddAsync(entity);

    public void Update(GuideApplication entity) => _db.GuideApplications.Update(entity);
    public void Delete(GuideApplication entity) => _db.GuideApplications.Remove(entity);
}
