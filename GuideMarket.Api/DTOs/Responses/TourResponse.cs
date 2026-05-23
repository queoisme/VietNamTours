namespace GuideMarket.Api.DTOs.Responses;

public class TourGuideInfo
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public string? AvatarUrl { get; set; }
}

public class ItineraryItem
{
    public string Time { get; set; } = default!;
    public string Activity { get; set; } = default!;
    public string? Description { get; set; }
}

public class TourListItemResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? CoverImageUrl { get; set; }
    public string LocationCity { get; set; } = default!;
    public string Category { get; set; } = default!;
    public decimal PricePerPerson { get; set; }
    public decimal DurationHours { get; set; }
    public short MaxGroupSize { get; set; }
    public decimal AvgRating { get; set; }
    public int TotalReviews { get; set; }
    public int TotalBookings { get; set; }
    public bool IsBoosted { get; set; }
    public string Status { get; set; } = default!;
    public string[] Images { get; set; } = [];
    public TourGuideInfo Guide { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}

public class TourResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string LocationCity { get; set; } = default!;
    public string? LocationAddress { get; set; }
    public decimal? Lat { get; set; }
    public decimal? Lng { get; set; }
    public decimal PricePerPerson { get; set; }
    public decimal DurationHours { get; set; }
    public short MaxGroupSize { get; set; }
    public string[] Highlights { get; set; } = [];
    public string[] Included { get; set; } = [];
    public string[] Excluded { get; set; } = [];
    public List<ItineraryItem> Itinerary { get; set; } = [];
    public string[] Images { get; set; } = [];
    public string? CoverImageUrl { get; set; }
    public string Status { get; set; } = default!;
    public decimal AvgRating { get; set; }
    public int TotalReviews { get; set; }
    public int TotalBookings { get; set; }
    public bool IsBoosted { get; set; }
    public DateTimeOffset? BoostExpiresAt { get; set; }
    public TourGuideInfo Guide { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public class TourAvailabilityResponse
{
    public Guid Id { get; set; }
    public Guid TourId { get; set; }
    public DateOnly AvailableDate { get; set; }
    public short MaxSlots { get; set; }
    public short BookedSlots { get; set; }
    public short AvailableSlots => (short)(MaxSlots - BookedSlots);
    public bool IsBlocked { get; set; }
}
