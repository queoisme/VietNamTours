using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IConversationRepository : IRepository<Conversation>
{
    Task<Conversation?> GetByIdForUserAsync(Guid conversationId, Guid userId);
    Task<Conversation?> GetByBookingIdAsync(Guid bookingId);
    Task<Conversation?> GetByCustomerAndTourAsync(Guid customerId, Guid tourId);
    Task<(List<Conversation> Items, long Total)> GetByUserIdAsync(Guid userId, int page, int size);
    Task<(List<Message> Items, long Total)> GetMessagesAsync(Guid conversationId, DateTimeOffset? before, int size);
    Task AddMessageAsync(Message message);
    Task MarkReadAsync(Guid conversationId, Guid userId);
}
