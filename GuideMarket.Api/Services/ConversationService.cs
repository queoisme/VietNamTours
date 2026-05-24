using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Services;

public class ConversationService : IConversationService
{
    private readonly IUnitOfWork _uow;

    public ConversationService(IUnitOfWork uow) => _uow = uow;

    public async Task<(List<ConversationListItemResponse> Items, long Total)> GetConversationsAsync(
        Guid userId, int page, int size)
    {
        try
        {
            var (items, total) = await _uow.Conversations.GetByUserIdAsync(userId, page, size);
            return (items.Select(c => MapList(c, userId)).ToList(), total);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot load conversations for user {userId}. {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<ConversationListItemResponse> GetConversationAsync(Guid userId, Guid conversationId)
    {
        try
        {
            var conv = await _uow.Conversations.GetByIdForUserAsync(conversationId, userId)
                ?? throw new KeyNotFoundException("Conversation not found or access denied");
            return MapList(conv, userId);
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot load conversation {conversationId}. {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    public async Task<(List<MessageResponse> Items, long Total)> GetMessagesAsync(
        Guid userId, Guid conversationId, DateTimeOffset? before, int size)
    {
        try
        {
            var conv = await _uow.Conversations.GetByIdForUserAsync(conversationId, userId)
                ?? throw new KeyNotFoundException("Conversation not found or access denied");

            var (items, total) = await _uow.Conversations.GetMessagesAsync(conversationId, before, size);
            return (items.Select(MapMessage).ToList(), total);
        }
        catch (KeyNotFoundException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Cannot load messages for conversation {conversationId}. {ex.InnerException?.Message ?? ex.Message}");
        }
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

        // If a pre-booking conversation already exists for this customer+tour, link booking to it
        var preBooking = await _uow.Conversations.GetByCustomerAndTourAsync(booking.CustomerId, booking.TourId);
        if (preBooking != null)
        {
            var tracked = await _uow.Conversations.FirstOrDefaultAsync(c => c.Id == preBooking.Id)
                          ?? preBooking;
            tracked.BookingId = bookingId;
            _uow.Conversations.Update(tracked);
            try
            {
                await _uow.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                var existingAfterError = await _uow.Conversations.GetByBookingIdAsync(bookingId);
                if (existingAfterError != null)
                    return MapList(existingAfterError, userId);
                throw;
            }
            var refreshed = await _uow.Conversations.GetByBookingIdAsync(bookingId);
            return MapList(refreshed!, userId);
        }

        var conv = new Conversation
        {
            Id         = Guid.NewGuid(),
            BookingId  = bookingId,
            TourId     = booking.TourId,
            CustomerId = booking.CustomerId,
            GuideId    = booking.GuideId,
            CreatedAt  = DateTimeOffset.UtcNow,
        };

        try
        {
            await _uow.Conversations.AddAsync(conv);
            await _uow.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var existingAfterError = await _uow.Conversations.GetByBookingIdAsync(bookingId);
            if (existingAfterError != null)
                return MapList(existingAfterError, userId);

            throw new InvalidOperationException(
                $"Cannot create conversation for booking {bookingId}. " +
                $"{ex.InnerException?.Message ?? ex.Message}");
        }

        conv.Booking   = booking;
        conv.Tour      = booking.Tour;
        conv.Customer  = booking.Customer;
        conv.Guide     = booking.Guide;

        return MapList(conv, userId);
    }

    public async Task<ConversationListItemResponse> GetOrCreateByTourAsync(Guid customerId, Guid tourId)
    {
        try
        {
            var tour = await _uow.Tours.GetByIdAsync(tourId)
                ?? throw new KeyNotFoundException("Tour not found");

            if (tour.GuideId == customerId)
                throw new ForbiddenAccessException("Guide cannot start a chat with their own tour");

            var existing = await _uow.Conversations.GetAnyByCustomerAndTourAsync(customerId, tourId);
            if (existing != null)
                return MapList(existing, customerId);

            var customer = await _uow.Users.GetByIdAsync(customerId)
                ?? throw new KeyNotFoundException("User not found");

            var guide = await _uow.Users.GetByIdAsync(tour.GuideId)
                ?? throw new KeyNotFoundException("Guide not found");

            var conv = new Conversation
            {
                Id         = Guid.NewGuid(),
                BookingId  = null,
                TourId     = tourId,
                CustomerId = customerId,
                GuideId    = tour.GuideId,
                CreatedAt  = DateTimeOffset.UtcNow,
            };

            try
            {
                await _uow.Conversations.AddAsync(conv);
                await _uow.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                var existingAfterError = await _uow.Conversations.GetAnyByCustomerAndTourAsync(customerId, tourId);
                if (existingAfterError != null)
                    return MapList(existingAfterError, customerId);

                throw new InvalidOperationException(
                    $"Cannot create conversation for tour {tourId}. " +
                    $"{ex.InnerException?.Message ?? ex.Message}");
            }

            conv.Tour     = tour;
            conv.Customer = customer;
            conv.Guide    = guide;

            return MapList(conv, customerId);
        }
        catch (KeyNotFoundException) { throw; }
        catch (ForbiddenAccessException) { throw; }
        catch (InvalidOperationException) { throw; }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"[by-tour] {ex.GetType().Name}: {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private static ConversationListItemResponse MapList(Conversation c, Guid userId)
    {
        var isCustomer  = c.CustomerId == userId;
        var otherUser   = isCustomer ? c.Guide : c.Customer;
        var otherUserId = otherUser?.Id ?? (isCustomer ? c.GuideId : c.CustomerId);
        var unread      = isCustomer ? c.CustomerUnread : c.GuideUnread;
        var tourTitle   = c.Booking?.Tour?.Title ?? c.Tour?.Title ?? string.Empty;
        return new(c.Id, c.BookingId, c.TourId, otherUserId,
            otherUser?.FullName ?? string.Empty,
            otherUser?.AvatarUrl,
            tourTitle,
            unread, c.LastMessagePreview, c.LastMessageAt, c.CreatedAt);
    }

    private static MessageResponse MapMessage(Message m) => new(
        m.Id, m.ConversationId, m.SenderId,
        m.Sender?.FullName ?? string.Empty,
        m.Sender?.AvatarUrl,
        m.Content, m.IsRead, m.ReadAt, m.SentAt);
}
