using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(Guid id) =>
        await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null);

    public async Task<User?> FirstOrDefaultAsync(Expression<Func<User, bool>> predicate) =>
        await _db.Users.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task<User?> GetByEmailAsync(string email) =>
        await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null);

    public async Task<bool> ExistsAsync(Guid id) =>
        await _db.Users.AnyAsync(u => u.Id == id && u.DeletedAt == null);

    public async Task<List<Guid>> GetAdminIdsAsync() =>
        await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.admin && u.DeletedAt == null)
            .Select(u => u.Id)
            .ToListAsync();

    public async Task AddAsync(User entity) => await _db.Users.AddAsync(entity);

    public void Update(User entity) => _db.Users.Update(entity);
    public void Delete(User entity) => _db.Users.Remove(entity);
}
