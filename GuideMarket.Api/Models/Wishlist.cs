using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("wishlists")]
public class Wishlist
{
    [Key][Column("id")] public Guid Id { get; set; }
    [Column("customer_id")] public Guid CustomerId { get; set; }
    [Column("tour_id")] public Guid TourId { get; set; }
    [Column("created_at")] public DateTimeOffset CreatedAt { get; set; }

    public User Customer { get; set; } = default!;
    public Tour Tour { get; set; } = default!;
}
