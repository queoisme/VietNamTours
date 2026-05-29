namespace GuideMarket.Api.DTOs.Responses;

public record SupportTicketResponse(
    Guid Id,
    Guid UserId,
    string UserName,
    string? UserAvatarUrl,
    string Subject,
    string Status,
    int UserUnread,
    int AdminUnread,
    string? LastMessagePreview,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset CreatedAt);

public record SupportMessageResponse(
    Guid Id,
    Guid SupportConversationId,
    Guid SenderId,
    string SenderName,
    string? SenderAvatarUrl,
    string Content,
    bool IsRead,
    DateTimeOffset SentAt,
    List<MessageAttachmentDto> Attachments);
