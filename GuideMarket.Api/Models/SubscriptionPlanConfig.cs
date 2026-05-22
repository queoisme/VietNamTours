namespace GuideMarket.Api.Models;

public class SubscriptionPlanConfig
{
    public string Plan { get; set; } = default!;       // "premium" | "pro"
    public decimal Price { get; set; }
    public int Days { get; set; }
    public string Description { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
}
