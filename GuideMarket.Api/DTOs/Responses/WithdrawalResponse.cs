namespace GuideMarket.Api.DTOs.Responses;

public record WithdrawalResponse(
    Guid Id,
    decimal Amount,
    decimal Fee,
    decimal NetAmount,
    string Method,
    string AccountInfo,
    string? Note,
    string Status,
    string? AdminNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt
);

public record FinanceResponse(
    decimal Balance,
    decimal TotalEarned,
    decimal TotalWithdrawn,
    string SubscriptionPlan,
    DateTimeOffset? SubscriptionExpiresAt
);

public record AdminWithdrawalResponse(
    Guid Id,
    Guid GuideId,
    string GuideName,
    string GuideEmail,
    decimal Amount,
    decimal Fee,
    decimal NetAmount,
    string Method,
    string AccountInfo,
    string? Note,
    string Status,
    string? AdminNote,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt
);
