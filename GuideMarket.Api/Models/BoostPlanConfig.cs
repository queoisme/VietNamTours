namespace GuideMarket.Api.Models;

public class BoostPlanConfig
{
    public string Plan { get; set; } = default!;       // "basic" | "standard" | "premium"
    public decimal Price { get; set; }
    public int Days { get; set; }
    public string Description { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
}
