namespace GuideMarket.Api.DTOs.Responses;

public record ReviewResponse(
    Guid Id,
    Guid BookingId,
    Guid TourId,
    string TourTitle,
    Guid CustomerId,
    string CustomerName,
    string? CustomerAvatarUrl,
    short Rating,
    string? Comment,
    string? GuideReply,
    bool IsVisible,
    DateTimeOffset CreatedAt
);
