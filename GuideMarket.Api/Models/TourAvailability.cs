using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("tour_availabilities")]
public class TourAvailability
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tour_id")]
    public Guid TourId { get; set; }

    [Column("available_date")]
    public DateOnly AvailableDate { get; set; }

    [Column("max_slots")]
    public short MaxSlots { get; set; }

    [Column("booked_slots")]
    public short BookedSlots { get; set; }

    [Column("is_blocked")]
    public bool IsBlocked { get; set; }

    [Column("blocked_by_booking_id")]
    public Guid? BlockedByBookingId { get; set; }

    public Tour Tour { get; set; } = default!;
}
