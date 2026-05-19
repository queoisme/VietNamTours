using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("conversations")]
public class Conversation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("booking_id")]
    public Guid BookingId { get; set; }

    [Column("customer_id")]
    public Guid CustomerId { get; set; }

    [Column("guide_id")]
    public Guid GuideId { get; set; }

    [Column("customer_unread")]
    public int CustomerUnread { get; set; }

    [Column("guide_unread")]
    public int GuideUnread { get; set; }

    [Column("last_message_at")]
    public DateTimeOffset? LastMessageAt { get; set; }

    [Column("last_message_preview")]
    public string? LastMessagePreview { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    public Booking Booking { get; set; } = default!;
    public User Customer { get; set; } = default!;
    public User Guide { get; set; } = default!;
}
