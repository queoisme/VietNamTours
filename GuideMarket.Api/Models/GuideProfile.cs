using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

public enum VerificationStatus { pending, approved, rejected }
public enum SubscriptionPlan { free, premium, pro }

[Table("guide_profiles")]
public class GuideProfile
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("bio")]
    public string? Bio { get; set; }

    [Column("experience_years")]
    public short ExperienceYears { get; set; }

    [Column("languages")]
    public string[] Languages { get; set; } = [];

    [Column("certifications", TypeName = "jsonb")]
    public string Certifications { get; set; } = "[]";

    [Column("identity_doc_url")]
    public string? IdentityDocUrl { get; set; }

    [Column("verification_status")]
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.pending;

    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("avg_rating")]
    public decimal AvgRating { get; set; }

    [Column("total_reviews")]
    public int TotalReviews { get; set; }

    [Column("subscription_plan")]
    public SubscriptionPlan SubscriptionPlan { get; set; } = SubscriptionPlan.free;

    [Column("subscription_expires_at")]
    public DateTimeOffset? SubscriptionExpiresAt { get; set; }

    [Column("balance")]
    public decimal Balance { get; set; }

    [Column("total_earned")]
    public decimal TotalEarned { get; set; }

    [Column("total_withdrawn")]
    public decimal TotalWithdrawn { get; set; }

    public User User { get; set; } = default!;
}
