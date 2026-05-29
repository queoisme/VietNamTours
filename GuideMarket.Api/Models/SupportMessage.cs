using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("support_messages")]
public class SupportMessage
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("support_conversation_id")]
    public Guid SupportConversationId { get; set; }

    [Column("sender_id")]
    public Guid SenderId { get; set; }

    [Column("content")]
    public string Content { get; set; } = string.Empty;

    [Column("attachments", TypeName = "jsonb")]
    public string Attachments { get; set; } = "[]";

    [Column("is_read")]
    public bool IsRead { get; set; }

    [Column("sent_at")]
    public DateTimeOffset SentAt { get; set; }

    public SupportConversation SupportConversation { get; set; } = default!;
    public User Sender { get; set; } = default!;
}
