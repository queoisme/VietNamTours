namespace GuideMarket.Api.Services.Interfaces;

public interface INotificationService
{
    /// <summary>Write in-app notification and optionally enqueue email via Hangfire.</summary>
    Task CreateAsync(
        Guid userId,
        string type,
        string title,
        string? body = null,
        string? entityType = null,
        Guid? entityId = null,
        string? emailSubject = null,
        string? emailBody = null);

    Task<(List<NotificationDto> Items, long Total)> GetByUserIdAsync(Guid userId, int page, int size);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task MarkReadAsync(Guid userId, Guid notificationId);
    Task MarkAllReadAsync(Guid userId);
}

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string? Body,
    string? EntityType,
    Guid? EntityId,
    bool IsRead,
    DateTimeOffset CreatedAt);
