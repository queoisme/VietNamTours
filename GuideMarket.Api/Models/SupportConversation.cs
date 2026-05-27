using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("support_conversations")]
public class SupportConversation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("subject")]
    public string Subject { get; set; } = default!;

    [Column("status")]
    public string Status { get; set; } = "open";

    [Column("user_unread")]
    public int UserUnread { get; set; }

    [Column("admin_unread")]
    public int AdminUnread { get; set; }

    [Column("last_message_at")]
    public DateTimeOffset? LastMessageAt { get; set; }

    [Column("last_message_preview")]
    public string? LastMessagePreview { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    public User User { get; set; } = default!;
}
