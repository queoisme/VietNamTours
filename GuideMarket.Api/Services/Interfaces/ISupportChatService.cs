using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface ISupportChatService
{
    Task<SupportTicketResponse> CreateTicketAsync(Guid userId, CreateSupportTicketRequest request);
    Task<(List<SupportTicketResponse> Items, long Total)> GetMyTicketsAsync(Guid userId, int page, int size);
    Task<SupportTicketResponse> GetTicketAsync(Guid userId, Guid ticketId, bool isAdmin);
    Task<(List<SupportMessageResponse> Items, long Total)> GetMessagesAsync(Guid userId, Guid ticketId, bool isAdmin, DateTimeOffset? before, int size);
    Task<SupportMessageResponse> SendMessageAsync(Guid senderId, Guid ticketId, SendSupportMessageRequest request, bool isAdmin);
    Task MarkReadAsync(Guid userId, Guid ticketId, bool isAdmin);
    Task<(List<SupportTicketResponse> Items, long Total)> GetAllTicketsAsync(int page, int size, string? status);
    Task<SupportTicketResponse> UpdateStatusAsync(Guid adminId, Guid ticketId, string status);
}
