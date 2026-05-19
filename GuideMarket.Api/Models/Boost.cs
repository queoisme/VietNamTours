using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

public enum BoostPlan { basic, standard, premium }
public enum BoostStatus { active, expired, cancelled }

[Table("boosts")]
public class Boost
{
    [Key][Column("id")] public Guid Id { get; set; }
    [Column("tour_id")] public Guid TourId { get; set; }
    [Column("guide_id")] public Guid GuideId { get; set; }
    [Column("plan")] public BoostPlan Plan { get; set; }
    [Column("price_paid")] public decimal PricePaid { get; set; }
    [Column("duration_days")] public short DurationDays { get; set; }
    [Column("starts_at")] public DateTimeOffset StartsAt { get; set; }
    [Column("expires_at")] public DateTimeOffset ExpiresAt { get; set; }
    [Column("payment_txn_id")] public string? PaymentTxnId { get; set; }
    [Column("status")] public BoostStatus Status { get; set; } = BoostStatus.cancelled;

    public Tour Tour { get; set; } = default!;
    public User Guide { get; set; } = default!;
}
