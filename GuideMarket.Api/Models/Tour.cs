using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

public enum TourCategory { nature, culture, food, resort, adventure, other }
public enum TourStatus { draft, active, inactive }
public enum TourType { @private, group }

[Table("tours")]
public class Tour
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("guide_id")]
    public Guid GuideId { get; set; }

    [Column("title")]
    public string Title { get; set; } = default!;

    [Column("slug")]
    public string Slug { get; set; } = default!;

    [Column("description")]
    public string Description { get; set; } = default!;

    [Column("category")]
    public TourCategory Category { get; set; }

    [Column("location_city")]
    public string LocationCity { get; set; } = default!;

    [Column("location_address")]
    public string? LocationAddress { get; set; }

    [Column("lat")]
    public decimal? Lat { get; set; }

    [Column("lng")]
    public decimal? Lng { get; set; }

    [Column("price_per_person")]
    public decimal PricePerPerson { get; set; }

    [Column("duration_hours")]
    public decimal DurationHours { get; set; }

    [Column("max_group_size")]
    public short MaxGroupSize { get; set; } = 10;

    [Column("highlights")]
    public string[] Highlights { get; set; } = [];

    [Column("included")]
    public string[] Included { get; set; } = [];

    [Column("excluded")]
    public string[] Excluded { get; set; } = [];

    [Column("itinerary", TypeName = "jsonb")]
    public string Itinerary { get; set; } = "[]";

    [Column("images")]
    public string[] Images { get; set; } = [];

    [Column("cover_image_url")]
    public string? CoverImageUrl { get; set; }

    [Column("status")]
    public TourStatus Status { get; set; } = TourStatus.draft;

    [Column("tour_type")]
    public TourType TourType { get; set; } = TourType.group;

    [Column("avg_rating")]
    public decimal AvgRating { get; set; }

    [Column("total_reviews")]
    public int TotalReviews { get; set; }

    [Column("total_bookings")]
    public int TotalBookings { get; set; }

    [Column("is_boosted")]
    public bool IsBoosted { get; set; }

    [Column("boost_expires_at")]
    public DateTimeOffset? BoostExpiresAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [Column("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }

    public User Guide { get; set; } = default!;
    public ICollection<TourAvailability> Availabilities { get; set; } = [];
}
