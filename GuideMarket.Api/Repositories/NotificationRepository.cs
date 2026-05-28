using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;

    public NotificationRepository(AppDbContext db) => _db = db;

    public async Task<Notification?> GetByIdAsync(Guid id) =>
        await _db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);

    public async Task<Notification?> FirstOrDefaultAsync(Expression<Func<Notification, bool>> predicate) =>
        await _db.Notifications.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(Notification entity) => await _db.Notifications.AddAsync(entity);

    public void Update(Notification entity) => _db.Notifications.Update(entity);
    public void Delete(Notification entity) => _db.Notifications.Remove(entity);

    public async Task<(List<Notification> Items, long Total)> GetByUserIdAsync(Guid userId, int page, int size, bool? isRead = null)
    {
        var query = _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId);

        if (isRead.HasValue)
            query = query.Where(n => n.IsRead == isRead.Value);

        var total       = await query.LongCountAsync();
        var clampedSize = Math.Clamp(size, 1, 100);
        var skip        = (Math.Max(page, 1) - 1) * clampedSize;
        var items       = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(skip).Take(clampedSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId) =>
        await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task<Notification?> GetByIdForUserAsync(Guid userId, Guid notificationId) =>
        await _db.Notifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

    public async Task MarkAllReadAsync(Guid userId) =>
        await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
}
