using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

public enum BookingStatus { pending, confirmed, completed, cancelled, rejected }
public enum PaymentStatus { unpaid, paid, refunded, refund_failed }
public enum CancellationBy { customer, guide, admin, system }

[Table("bookings")]
public class Booking
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tour_id")]
    public Guid TourId { get; set; }

    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    [Column("guide_id")]
    public Guid GuideId { get; set; }

    [Column("tour_date")]
    public DateOnly TourDate { get; set; }

    [Column("num_people")]
    public short NumPeople { get; set; }

    [Column("total_price")]
    public decimal TotalPrice { get; set; }

    [Column("contact_name")]
    public string ContactName { get; set; } = default!;

    [Column("contact_phone")]
    public string ContactPhone { get; set; } = default!;

    [Column("contact_email")]
    public string? ContactEmail { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("status")]
    public BookingStatus Status { get; set; } = BookingStatus.pending;

    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("cancellation_by")]
    public CancellationBy? CancellationBy { get; set; }

    [Column("cancellation_reason")]
    public string? CancellationReason { get; set; }

    [Column("refund_amount")]
    public decimal RefundAmount { get; set; }

    [Column("refund_policy")]
    public string? RefundPolicy { get; set; }

    [Column("payment_status")]
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.unpaid;

    [Column("payment_method")]
    public string? PaymentMethod { get; set; }

    [Column("payment_txn_id")]
    public string? PaymentTxnId { get; set; }

    [Column("payment_paid_at")]
    public DateTimeOffset? PaymentPaidAt { get; set; }

    [Column("vnpay_transaction_no")]
    public string? VnpayTransactionNo { get; set; }

    [Column("confirmed_at")]
    public DateTimeOffset? ConfirmedAt { get; set; }

    [Column("completed_at")]
    public DateTimeOffset? CompletedAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    public Tour Tour { get; set; } = default!;
    public User Customer { get; set; } = default!;
    public User Guide { get; set; } = default!;
    public Conversation? Conversation { get; set; }
}
