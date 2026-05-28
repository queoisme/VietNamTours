using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<bool> ExistsAsync(Guid id);
    Task<List<Guid>> GetAdminIdsAsync();
}
