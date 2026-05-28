using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class SupportChatService : ISupportChatService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;

    public SupportChatService(IUnitOfWork uow, INotificationService notifications)
    {
        _uow           = uow;
        _notifications = notifications;
    }

    public async Task<SupportTicketResponse> CreateTicketAsync(Guid userId, CreateSupportTicketRequest request)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");

        var conv = new SupportConversation
        {
            Id        = Guid.NewGuid(),
            UserId    = userId,
            Subject   = request.Subject,
            Status    = "open",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _uow.Support.AddAsync(conv);
        await _uow.SaveChangesAsync();

        var created = await _uow.Support.GetConversationByIdAsync(conv.Id)
            ?? throw new KeyNotFoundException("Ticket not found after creation");

        await _notifications.NotifyAdminsAsync(
            "support_ticket_created",
            "Ticket hỗ trợ mới",
            $"{user.FullName} mở ticket: {request.Subject}",
            "support",
            conv.Id);

        return Map(created);
    }

    public async Task<(List<SupportTicketResponse> Items, long Total)> GetMyTicketsAsync(Guid userId, int page, int size)
    {
        var (items, total) = await _uow.Support.GetByUserIdAsync(userId, page, size);
        return (items.Select(Map).ToList(), total);
    }

    public async Task<SupportTicketResponse> GetTicketAsync(Guid userId, Guid ticketId, bool isAdmin)
    {
        var conv = await _uow.Support.GetConversationByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");

        if (!isAdmin && conv.UserId != userId)
            throw new ForbiddenAccessException("Not your ticket");

        return Map(conv);
    }

    public async Task<(List<SupportMessageResponse> Items, long Total)> GetMessagesAsync(
        Guid userId, Guid ticketId, bool isAdmin, DateTimeOffset? before, int size)
    {
        var conv = await _uow.Support.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");

        if (!isAdmin && conv.UserId != userId)
            throw new ForbiddenAccessException("Not your ticket");

        var (items, total) = await _uow.Support.GetMessagesAsync(ticketId, before, size);
        return (items.Select(MapMessage).ToList(), total);
    }

    public async Task<SupportMessageResponse> SendMessageAsync(
        Guid senderId, Guid ticketId, SendSupportMessageRequest request, bool isAdmin)
    {
        var conv = await _uow.Support.GetConversationByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");

        if (!isAdmin && conv.UserId != senderId)
            throw new ForbiddenAccessException("Not your ticket");

        if (conv.Status == "closed")
            throw new InvalidOperationException("Ticket is closed");

        var msg = new SupportMessage
        {
            Id                    = Guid.NewGuid(),
            SupportConversationId = ticketId,
            SenderId              = senderId,
            Content               = request.Content,
            SentAt                = DateTimeOffset.UtcNow,
        };

        await _uow.Support.AddMessageAsync(msg);

        // Update conversation metadata
        var trackingConv = await _uow.Support.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");
        _uow.Support.Update(trackingConv);

        trackingConv.LastMessageAt      = msg.SentAt;
        trackingConv.LastMessagePreview = msg.Content.Length > 200
            ? msg.Content[..200]
            : msg.Content;

        if (isAdmin)
        {
            trackingConv.UserUnread++;
            if (trackingConv.Status == "open")
                trackingConv.Status = "in_progress";
        }
        else
        {
            trackingConv.AdminUnread++;
        }

        await _uow.SaveChangesAsync();

        // Reload sender for response
        var sender = await _uow.Users.GetByIdAsync(senderId)
            ?? throw new KeyNotFoundException("Sender not found");
        msg.Sender = sender;

        if (isAdmin)
        {
            await _notifications.CreateAsync(
                conv.UserId,
                "support_reply",
                "Admin đã trả lời ticket của bạn",
                $"Ticket \"{conv.Subject}\": {msg.Content[..Math.Min(msg.Content.Length, 100)]}",
                "support",
                ticketId);
        }
        else
        {
            await _notifications.NotifyAdminsAsync(
                "support_message",
                "Tin nhắn mới trong ticket hỗ trợ",
                $"{sender.FullName}: {msg.Content[..Math.Min(msg.Content.Length, 100)]}",
                "support",
                ticketId);
        }

        return MapMessage(msg);
    }

    public async Task MarkReadAsync(Guid userId, Guid ticketId, bool isAdmin)
    {
        var conv = await _uow.Support.GetByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");

        if (!isAdmin && conv.UserId != userId)
            throw new ForbiddenAccessException("Not your ticket");

        if (isAdmin)
            await _uow.Support.MarkAdminReadAsync(ticketId);
        else
            await _uow.Support.MarkUserReadAsync(ticketId);

        await _uow.SaveChangesAsync();
    }

    public async Task<(List<SupportTicketResponse> Items, long Total)> GetAllTicketsAsync(int page, int size, string? status)
    {
        var (items, total) = await _uow.Support.GetAllAsync(page, size, status);
        return (items.Select(Map).ToList(), total);
    }

    public async Task<SupportTicketResponse> UpdateStatusAsync(Guid adminId, Guid ticketId, string status)
    {
        var admin = await _uow.Users.GetByIdAsync(adminId)
            ?? throw new KeyNotFoundException("User not found");
        if (admin.Role != UserRole.admin)
            throw new ForbiddenAccessException("Only admins can update ticket status");
        var validStatuses = new[] { "open", "in_progress", "resolved", "closed" };
        if (!validStatuses.Contains(status))
            throw new InvalidOperationException($"Invalid status: {status}");

        var conv = await _uow.Support.GetConversationByIdAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket not found");

        _uow.Support.Update(conv);
        conv.Status = status;
        await _uow.SaveChangesAsync();

        return Map(conv);
    }

    private static SupportTicketResponse Map(SupportConversation c) => new(
        c.Id, c.UserId,
        c.User?.FullName ?? string.Empty,
        c.User?.AvatarUrl,
        c.Subject, c.Status,
        c.UserUnread, c.AdminUnread,
        c.LastMessagePreview, c.LastMessageAt,
        c.CreatedAt);

    private static SupportMessageResponse MapMessage(SupportMessage m) => new(
        m.Id, m.SupportConversationId, m.SenderId,
        m.Sender?.FullName ?? string.Empty,
        m.Sender?.AvatarUrl,
        m.Content, m.IsRead, m.SentAt);
}
