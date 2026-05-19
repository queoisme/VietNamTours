using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("subscriptions")]
public class Subscription
{
    [Key][Column("id")] public Guid Id { get; set; }
    [Column("guide_id")] public Guid GuideId { get; set; }
    [Column("plan")] public SubscriptionPlan Plan { get; set; }
    [Column("price_paid")] public decimal PricePaid { get; set; }
    [Column("starts_at")] public DateTimeOffset StartsAt { get; set; }
    [Column("expires_at")] public DateTimeOffset ExpiresAt { get; set; }
    [Column("payment_txn_id")] public string? PaymentTxnId { get; set; }
    [Column("status")] public BoostStatus Status { get; set; } = BoostStatus.cancelled;

    public User Guide { get; set; } = default!;
}
