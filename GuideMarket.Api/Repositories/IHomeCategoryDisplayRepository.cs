using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IHomeCategoryDisplayRepository
{
    Task<List<HomeCategoryDisplay>> GetAllVisibleAsync();
    Task<List<HomeCategoryDisplay>> GetAllAsync();
    Task<HomeCategoryDisplay?> GetByIdAsync(int id);
    Task AddAsync(HomeCategoryDisplay entity);
    void Update(HomeCategoryDisplay entity);
    void Remove(HomeCategoryDisplay entity);
}
