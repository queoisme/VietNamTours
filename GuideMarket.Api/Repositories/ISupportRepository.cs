using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface ISupportRepository : IRepository<SupportConversation>
{
    Task<SupportConversation?> GetConversationByIdAsync(Guid id);
    Task<(List<SupportConversation> Items, long Total)> GetByUserIdAsync(Guid userId, int page, int size);
    Task<(List<SupportConversation> Items, long Total)> GetAllAsync(int page, int size, string? status);
    Task AddMessageAsync(SupportMessage message);
    Task<(List<SupportMessage> Items, long Total)> GetMessagesAsync(Guid convId, DateTimeOffset? before, int size);
    Task MarkUserReadAsync(Guid convId);
    Task MarkAdminReadAsync(Guid convId);
}
