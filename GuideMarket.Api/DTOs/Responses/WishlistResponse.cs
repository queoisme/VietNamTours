namespace GuideMarket.Api.DTOs.Responses;

public record WishlistItemResponse(
    Guid Id,
    Guid TourId,
    string TourTitle,
    string? TourCoverImageUrl,
    string TourCity,
    decimal PricePerPerson,
    decimal AvgRating,
    int TotalReviews,
    DateTimeOffset AddedAt
);
