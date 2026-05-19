using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IGuideProfileRepository : IRepository<GuideProfile>
{
    Task<GuideProfile?> GetByUserIdAsync(Guid userId);
    Task<GuideProfile?> GetByUserIdWithUserAsync(Guid userId);
    Task<bool> ExistsByUserIdAsync(Guid userId);
}
