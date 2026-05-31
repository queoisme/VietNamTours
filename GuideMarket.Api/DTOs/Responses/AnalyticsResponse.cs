namespace GuideMarket.Api.DTOs.Responses;

public class AdminSearchAnalyticsResponse
{
    public int TotalSearches { get; set; }
    public List<DailyCountDto> DailyCounts { get; set; } = [];
    public List<LabelCountDto> TopCategories { get; set; } = [];
    public List<LabelCountDto> TopCities { get; set; } = [];
    public List<LabelCountDto> TopKeywords { get; set; } = [];
    public List<LabelCountDto> PriceRangeCounts { get; set; } = [];
}

public class AdminPageViewAnalyticsResponse
{
    public int TotalViews { get; set; }
    public List<DailyCountDto> DailyCounts { get; set; } = [];
    public List<LabelCountDto> TopPages { get; set; } = [];
}

public record DailyCountDto(string Date, int Count);
public record LabelCountDto(string Label, int Count);
