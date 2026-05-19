using GuideMarket.Api.BackgroundJobs;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;
using Hangfire;

namespace GuideMarket.Api.Services;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IBackgroundJobClient _jobs;

    public NotificationService(IUnitOfWork uow, IBackgroundJobClient jobs)
    {
        _uow  = uow;
        _jobs = jobs;
    }

    public async Task CreateAsync(
        Guid userId,
        string type,
        string title,
        string? body = null,
        string? entityType = null,
        Guid? entityId = null,
        string? emailSubject = null,
        string? emailBody = null)
    {
        var notification = new Notification
        {
            Id         = Guid.NewGuid(),
            UserId     = userId,
            Type       = type,
            Title      = title,
            Body       = body,
            EntityType = entityType,
            EntityId   = entityId,
            IsRead     = false,
            CreatedAt  = DateTimeOffset.UtcNow,
        };

        await _uow.Notifications.AddAsync(notification);
        await _uow.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(emailSubject) && !string.IsNullOrWhiteSpace(emailBody))
        {
            var user = await _uow.Users.GetByIdAsync(userId);
            if (user?.Email is not null)
                _jobs.Enqueue<SendEmailJob>(x => x.ExecuteAsync(user.Email, emailSubject, emailBody));
        }
    }

    public async Task<(List<NotificationDto> Items, long Total)> GetByUserIdAsync(
        Guid userId, int page, int size)
    {
        var (items, total) = await _uow.Notifications.GetByUserIdAsync(userId, page, size);
        return (items.Select(Map).ToList(), total);
    }

    public Task<int> GetUnreadCountAsync(Guid userId) =>
        _uow.Notifications.GetUnreadCountAsync(userId);

    public async Task MarkReadAsync(Guid userId, Guid notificationId)
    {
        var n = await _uow.Notifications.GetByIdForUserAsync(userId, notificationId)
            ?? throw new KeyNotFoundException("Notification not found");

        if (!n.IsRead)
        {
            n.IsRead = true;
            _uow.Notifications.Update(n);
            await _uow.SaveChangesAsync();
        }
    }

    public Task MarkAllReadAsync(Guid userId) =>
        _uow.Notifications.MarkAllReadAsync(userId);

    private static NotificationDto Map(Notification n) => new(
        n.Id, n.Type, n.Title, n.Body, n.EntityType, n.EntityId, n.IsRead, n.CreatedAt);
}
