using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IConversationService
{
    Task<(List<ConversationListItemResponse> Items, long Total)> GetConversationsAsync(Guid userId, int page, int size);
    Task<ConversationListItemResponse> GetConversationAsync(Guid userId, Guid conversationId);
    Task<(List<MessageResponse> Items, long Total)> GetMessagesAsync(Guid userId, Guid conversationId, DateTimeOffset? before, int size);
    Task<MessageResponse> SendMessageAsync(Guid userId, Guid conversationId, SendMessageRequest request);
    Task MarkReadAsync(Guid userId, Guid conversationId);
    Task<ConversationListItemResponse> GetOrCreateByBookingAsync(Guid userId, Guid bookingId);
    Task<ConversationListItemResponse> GetOrCreateByTourAsync(Guid customerId, Guid tourId);
}
