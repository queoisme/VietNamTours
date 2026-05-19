using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IGuideApplicationRepository : IRepository<GuideApplication>
{
    Task<GuideApplication?> GetByIdWithUsersAsync(Guid id);
    Task<List<GuideApplication>> GetByUserIdAsync(Guid userId);
    Task<bool> HasPendingOrApprovedAsync(Guid userId);
    Task<(List<GuideApplication> Items, long Total)> GetAllAsync(GuideApplicationListParams p);
}
