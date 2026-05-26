using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IBoostPlanConfigRepository
{
    Task<List<BoostPlanConfig>> GetAllActiveAsync();
    Task<BoostPlanConfig?> GetByPlanAsync(string plan);
    void Update(BoostPlanConfig config);
}
