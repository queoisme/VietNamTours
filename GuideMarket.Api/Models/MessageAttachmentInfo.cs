namespace GuideMarket.Api.Models;

/// <summary>Metadata cho 1 file đính kèm trong tin nhắn. Serialized as JSONB array.</summary>
public record MessageAttachmentInfo(
    string Url,
    string FileName,
    long   FileSize,
    string ContentType
);
