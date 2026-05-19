using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface INotificationRepository : IRepository<Notification>
{
    Task<(List<Notification> Items, long Total)> GetByUserIdAsync(Guid userId, int page, int size);
    Task<int> GetUnreadCountAsync(Guid userId);
    Task<Notification?> GetByIdForUserAsync(Guid userId, Guid notificationId);
    Task MarkAllReadAsync(Guid userId);
}
