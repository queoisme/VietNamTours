using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class ConversationService : IConversationService
{
    private readonly IUnitOfWork _uow;

    public ConversationService(IUnitOfWork uow) => _uow = uow;

    public async Task<(List<ConversationListItemResponse> Items, long Total)> GetConversationsAsync(
        Guid userId, int page, int size)
    {
        var (items, total) = await _uow.Conversations.GetByUserIdAsync(userId, page, size);
        return (items.Select(c => MapList(c, userId)).ToList(), total);
    }

    public async Task<ConversationListItemResponse> GetConversationAsync(Guid userId, Guid conversationId)
    {
        var conv = await _uow.Conversations.GetByIdForUserAsync(conversationId, userId)
            ?? throw new KeyNotFoundException("Conversation not found or access denied");
        return MapList(conv, userId);
    }

    public async Task<(List<MessageResponse> Items, long Total)> GetMessagesAsync(
        Guid userId, Guid conversationId, DateTimeOffset? before, int size)
    {
        var conv = await _uow.Conversations.GetByIdForUserAsync(conversationId, userId)
            ?? throw new KeyNotFoundException("Conversation not found or access denied");

        var (items, total) = await _uow.Conversations.GetMessagesAsync(conversationId, before, size);
        return (items.Select(MapMessage).ToList(), total);
    }

    public async Task<MessageResponse> SendMessageAsync(Guid userId, Guid conversationId, SendMessageRequest request)
    {
        var conv = await _uow.Conversations.GetByIdForUserAsync(conversationId, userId)
            ?? throw new KeyNotFoundException("Conversation not found or access denied");

        var sender = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");

        var message = new Message
        {
            Id             = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId       = userId,
            Content        = request.Content,
            IsRead         = false,
            SentAt         = DateTimeOffset.UtcNow,
        };

        await _uow.Conversations.AddMessageAsync(message);
        await _uow.SaveChangesAsync();

        message.Sender = sender;
        return MapMessage(message);
    }

    public async Task MarkReadAsync(Guid userId, Guid conversationId)
    {
        _ = await _uow.Conversations.GetByIdForUserAsync(conversationId, userId)
            ?? throw new KeyNotFoundException("Conversation not found or access denied");

        await _uow.Conversations.MarkReadAsync(conversationId, userId);
        await _uow.SaveChangesAsync();
    }

    public async Task<ConversationListItemResponse> GetOrCreateByBookingAsync(Guid userId, Guid bookingId)
    {
        var booking = await _uow.Bookings.GetByIdWithDetailsAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.CustomerId != userId && booking.GuideId != userId)
            throw new ForbiddenAccessException("Access denied");

        var existing = await _uow.Conversations.GetByBookingIdAsync(bookingId);
        if (existing != null)
            return MapList(existing, userId);

        var conv = new Conversation
        {
            Id         = Guid.NewGuid(),
            BookingId  = bookingId,
            CustomerId = booking.CustomerId,
            GuideId    = booking.GuideId,
            CreatedAt  = DateTimeOffset.UtcNow,
        };

        await _uow.Conversations.AddAsync(conv);
        await _uow.SaveChangesAsync();

        conv.Booking   = booking;
        conv.Customer  = booking.Customer;
        conv.Guide     = booking.Guide;

        return MapList(conv, userId);
    }

    private static ConversationListItemResponse MapList(Conversation c, Guid userId)
    {
        var isCustomer   = c.CustomerId == userId;
        var otherUser    = isCustomer ? c.Guide : c.Customer;
        var otherUserId  = otherUser?.Id ?? (isCustomer ? c.GuideId : c.CustomerId);
        var unread       = isCustomer ? c.CustomerUnread : c.GuideUnread;
        return new(c.Id, c.BookingId, otherUserId,
            otherUser?.FullName ?? string.Empty,
            otherUser?.AvatarUrl,
            c.Booking?.Tour?.Title ?? string.Empty,
            unread, c.LastMessagePreview, c.LastMessageAt, c.CreatedAt);
    }

    private static MessageResponse MapMessage(Message m) => new(
        m.Id, m.ConversationId, m.SenderId,
        m.Sender?.FullName ?? string.Empty,
        m.Sender?.AvatarUrl,
        m.Content, m.IsRead, m.ReadAt, m.SentAt);
}
