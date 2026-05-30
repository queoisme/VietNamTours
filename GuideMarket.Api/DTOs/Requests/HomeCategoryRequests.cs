namespace GuideMarket.Api.DTOs.Requests;

public class CreateHomeCategoryRequest
{
    public string Name { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string CategoryFilter { get; set; } = default!;
    public bool IsVisible { get; set; } = true;
    public short SortOrder { get; set; }
}

public class UpdateHomeCategoryRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? CategoryFilter { get; set; }
    public bool? IsVisible { get; set; }
    public short? SortOrder { get; set; }
}
