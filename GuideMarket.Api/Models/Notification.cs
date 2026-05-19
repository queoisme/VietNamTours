using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("notifications")]
public class Notification
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("type")]
    public string Type { get; set; } = default!;

    [Column("title")]
    public string Title { get; set; } = default!;

    [Column("body")]
    public string? Body { get; set; }

    [Column("entity_type")]
    public string? EntityType { get; set; }

    [Column("entity_id")]
    public Guid? EntityId { get; set; }

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = default!;
}
