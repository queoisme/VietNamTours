using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class SupportRepository : ISupportRepository
{
    private readonly AppDbContext _db;

    public SupportRepository(AppDbContext db) => _db = db;

    public Task<SupportConversation?> GetByIdAsync(Guid id) =>
        _db.SupportConversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

    public Task<SupportConversation?> FirstOrDefaultAsync(Expression<Func<SupportConversation, bool>> predicate) =>
        _db.SupportConversations.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(SupportConversation entity) => await _db.SupportConversations.AddAsync(entity);
    public void Update(SupportConversation entity) => _db.SupportConversations.Update(entity);
    public void Delete(SupportConversation entity) => _db.SupportConversations.Remove(entity);

    public Task<SupportConversation?> GetConversationByIdAsync(Guid id) =>
        _db.SupportConversations.AsNoTracking()
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id);

    public async Task<(List<SupportConversation> Items, long Total)> GetByUserIdAsync(Guid userId, int page, int size)
    {
        var q = _db.SupportConversations.AsNoTracking()
            .Where(c => c.UserId == userId)
            .Include(c => c.User)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt);

        var total = await q.LongCountAsync();
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }

    public async Task<(List<SupportConversation> Items, long Total)> GetAllAsync(int page, int size, string? status)
    {
        var q = _db.SupportConversations.AsNoTracking()
            .Include(c => c.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
            q = q.Where(c => c.Status == status);

        q = q.OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt);

        var total = await q.LongCountAsync();
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }

    public async Task AddMessageAsync(SupportMessage message) =>
        await _db.SupportMessages.AddAsync(message);

    public async Task<(List<SupportMessage> Items, long Total)> GetMessagesAsync(
        Guid convId, DateTimeOffset? before, int size)
    {
        var total = await _db.SupportMessages.AsNoTracking()
            .LongCountAsync(m => m.SupportConversationId == convId);

        var q = _db.SupportMessages.AsNoTracking()
            .Where(m => m.SupportConversationId == convId)
            .AsQueryable();

        if (before.HasValue)
            q = q.Where(m => m.SentAt < before.Value);

        var items = await q
            .Include(m => m.Sender)
            .OrderByDescending(m => m.SentAt)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task MarkUserReadAsync(Guid convId)
    {
        var conv = await _db.SupportConversations.FindAsync(convId);
        if (conv == null) return;
        conv.UserUnread = 0;
        await _db.SupportMessages
            .Where(m => m.SupportConversationId == convId && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
    }

    public async Task MarkAdminReadAsync(Guid convId)
    {
        var conv = await _db.SupportConversations.FindAsync(convId);
        if (conv == null) return;
        conv.AdminUnread = 0;
        await _db.SupportMessages
            .Where(m => m.SupportConversationId == convId && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
    }
}
