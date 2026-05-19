namespace GuideMarket.Api.DTOs.Responses;

public class AdminStatsResponse
{
    public int TotalUsers { get; set; }
    public int TotalGuides { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalTours { get; set; }
    public int ActiveTours { get; set; }
    public int TotalBookings { get; set; }
    public int PendingBookings { get; set; }
    public int ConfirmedBookings { get; set; }
    public int CompletedBookings { get; set; }
    public int CancelledBookings { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class AdminRevenueItem
{
    public DateOnly Date { get; set; }
    public int BookingCount { get; set; }
    public decimal Revenue { get; set; }
}

public class AdminRevenueResponse
{
    public decimal TotalRevenue { get; set; }
    public int TotalBookings { get; set; }
    public List<AdminRevenueItem> ByDate { get; set; } = [];
}

public class AdminUserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = default!;
    public bool IsVerified { get; set; }
    public bool IsBanned { get; set; }
    public string? BanReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class AdminTourResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string LocationCity { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Status { get; set; } = default!;
    public decimal PricePerPerson { get; set; }
    public decimal AvgRating { get; set; }
    public int TotalBookings { get; set; }
    public bool IsBoosted { get; set; }
    public Guid GuideId { get; set; }
    public string GuideName { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
