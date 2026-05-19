using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("reviews")]
public class Review
{
    [Key][Column("id")] public Guid Id { get; set; }
    [Column("booking_id")] public Guid BookingId { get; set; }
    [Column("tour_id")] public Guid TourId { get; set; }
    [Column("customer_id")] public Guid CustomerId { get; set; }
    [Column("guide_id")] public Guid GuideId { get; set; }
    [Column("rating")] public short Rating { get; set; }
    [Column("comment")] public string? Comment { get; set; }
    [Column("guide_reply")] public string? GuideReply { get; set; }
    [Column("is_visible")] public bool IsVisible { get; set; } = true;
    [Column("created_at")] public DateTimeOffset CreatedAt { get; set; }

    public Booking Booking { get; set; } = default!;
    public Tour Tour { get; set; } = default!;
    public User Customer { get; set; } = default!;
    public User Guide { get; set; } = default!;
}
