using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("home_category_displays")]
public class HomeCategoryDisplay
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = default!;

    [Column("description")]
    public string Description { get; set; } = default!;

    [Column("category_filter")]
    public TourCategory CategoryFilter { get; set; }

    [Column("is_visible")]
    public bool IsVisible { get; set; } = true;

    [Column("sort_order")]
    public short SortOrder { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
