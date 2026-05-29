namespace GuideMarket.Api.DTOs.Responses;

public record ConversationListItemResponse(
    Guid Id,
    Guid? BookingId,
    Guid? TourId,
    Guid OtherUserId,
    string OtherUserName,
    string? OtherUserAvatarUrl,
    string TourTitle,
    int UnreadCount,
    string? LastMessagePreview,
    DateTimeOffset? LastMessageAt,
    DateTimeOffset CreatedAt
);

/// <summary>Result returned by POST /uploads/chat-attachment.</summary>
public record AttachmentUploadResult(
    string Url,
    string FileName,
    long   FileSize,
    string ContentType
);

/// <summary>Attachment metadata returned in message responses.</summary>
public record MessageAttachmentDto(
    string Url,
    string FileName,
    long   FileSize,
    string ContentType
);

public record MessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string? SenderAvatarUrl,
    string Content,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset SentAt,
    List<MessageAttachmentDto> Attachments
);
