using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("search_events")]
public class SearchEvent
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("query")]
    public string? Query { get; set; }

    [Column("category")]
    public string? Category { get; set; }

    [Column("location_city")]
    public string? LocationCity { get; set; }

    [Column("min_price")]
    public decimal? MinPrice { get; set; }

    [Column("max_price")]
    public decimal? MaxPrice { get; set; }

    [Column("result_count")]
    public int ResultCount { get; set; }

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
