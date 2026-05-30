namespace GuideMarket.Api.DTOs.Responses;

public class HomeCategoryResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string CategoryFilter { get; set; } = default!;
    public bool IsVisible { get; set; }
    public short SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
