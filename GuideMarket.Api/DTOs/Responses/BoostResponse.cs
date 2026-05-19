namespace GuideMarket.Api.DTOs.Responses;

public record BoostPlanInfo(string Plan, decimal Price, int DurationDays, string Description);

public record BoostResponse(
    Guid Id,
    Guid TourId,
    string TourTitle,
    string Plan,
    decimal PricePaid,
    int DurationDays,
    DateTimeOffset StartsAt,
    DateTimeOffset ExpiresAt,
    string Status
);

public record SubscriptionPlanInfo(string Plan, decimal Price, int DurationDays, string Description);

public record SubscriptionResponse(
    Guid Id,
    string Plan,
    decimal PricePaid,
    DateTimeOffset StartsAt,
    DateTimeOffset ExpiresAt,
    string Status
);
