using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

[Table("messages")]
public class Message
{
    [Key][Column("id")] public Guid Id { get; set; }
    [Column("conversation_id")] public Guid ConversationId { get; set; }
    [Column("sender_id")] public Guid SenderId { get; set; }
    [Column("content")] public string Content { get; set; } = default!;
    [Column("is_read")] public bool IsRead { get; set; }
    [Column("read_at")] public DateTimeOffset? ReadAt { get; set; }
    [Column("sent_at")] public DateTimeOffset SentAt { get; set; }

    public Conversation Conversation { get; set; } = default!;
    public User Sender { get; set; } = default!;
}
