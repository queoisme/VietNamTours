namespace GuideMarket.Api.DTOs.Responses;

public class CertificationItem
{
    public string Name { get; set; } = default!;
    public string IssuedBy { get; set; } = default!;
    public int Year { get; set; }
}

/// <summary>Public view — no financial / identity data.</summary>
public class GuidePublicResponse
{
    public Guid UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public short ExperienceYears { get; set; }
    public string[] Languages { get; set; } = [];
    public List<CertificationItem> Certifications { get; set; } = [];
    public decimal AvgRating { get; set; }
    public int TotalReviews { get; set; }
    public string SubscriptionPlan { get; set; } = default!;
}

/// <summary>Owner/admin view — includes financial and verification info.</summary>
public class GuideProfileResponse : GuidePublicResponse
{
    public Guid ProfileId { get; set; }
    public string VerificationStatus { get; set; } = default!;
    public string? RejectionReason { get; set; }
    public decimal Balance { get; set; }
    public decimal TotalEarned { get; set; }
    public decimal TotalWithdrawn { get; set; }
    public DateTimeOffset? SubscriptionExpiresAt { get; set; }
}
