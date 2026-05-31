using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("page_views")]
public class PageView
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("path")]
    public string Path { get; set; } = default!;

    [Column("user_id")]
    public Guid? UserId { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
