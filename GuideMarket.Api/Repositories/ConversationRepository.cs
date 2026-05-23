using System.Linq.Expressions;
using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly AppDbContext _db;

    public ConversationRepository(AppDbContext db) => _db = db;

    public Task<Conversation?> GetByIdAsync(Guid id) =>
        _db.Conversations.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);

    public Task<Conversation?> FirstOrDefaultAsync(Expression<Func<Conversation, bool>> predicate) =>
        _db.Conversations.AsNoTracking().FirstOrDefaultAsync(predicate);

    public async Task AddAsync(Conversation entity) => await _db.Conversations.AddAsync(entity);
    public void Update(Conversation entity) => _db.Conversations.Update(entity);
    public void Delete(Conversation entity) => _db.Conversations.Remove(entity);

    public Task<Conversation?> GetByIdForUserAsync(Guid conversationId, Guid userId) =>
        _db.Conversations.AsNoTracking()
            .Include(c => c.Booking).ThenInclude(b => b.Tour)
            .Include(c => c.Customer)
            .Include(c => c.Guide)
            .FirstOrDefaultAsync(c => c.Id == conversationId
                && (c.CustomerId == userId || c.GuideId == userId));

    public Task<Conversation?> GetByBookingIdAsync(Guid bookingId) =>
        _db.Conversations.AsNoTracking()
            .Include(c => c.Booking).ThenInclude(b => b.Tour)
            .Include(c => c.Customer)
            .Include(c => c.Guide)
            .FirstOrDefaultAsync(c => c.BookingId == bookingId);

    public async Task<(List<Conversation> Items, long Total)> GetByUserIdAsync(Guid userId, int page, int size)
    {
        var q = _db.Conversations.AsNoTracking()
            .Where(c => c.CustomerId == userId || c.GuideId == userId)
            .Include(c => c.Booking).ThenInclude(b => b.Tour)
            .Include(c => c.Customer)
            .Include(c => c.Guide)
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt);

        var total = await q.LongCountAsync();
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync();
        return (items, total);
    }

    public async Task<(List<Message> Items, long Total)> GetMessagesAsync(
        Guid conversationId, DateTimeOffset? before, int size)
    {
        var total = await _db.Messages.AsNoTracking()
            .LongCountAsync(m => m.ConversationId == conversationId);

        var q = _db.Messages.AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
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

    public async Task AddMessageAsync(Message message) =>
        await _db.Messages.AddAsync(message);

    public async Task MarkReadAsync(Guid conversationId, Guid userId)
    {
        var conv = await _db.Conversations.FindAsync(conversationId);
        if (conv == null) return;

        if (conv.CustomerId == userId)
            conv.CustomerUnread = 0;
        else if (conv.GuideId == userId)
            conv.GuideUnread = 0;

        await _db.Messages
            .Where(m => m.ConversationId == conversationId && m.SenderId != userId && !m.IsRead)
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.IsRead, true)
                .SetProperty(m => m.ReadAt, DateTimeOffset.UtcNow));
    }
}
