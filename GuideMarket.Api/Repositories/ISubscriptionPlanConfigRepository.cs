using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface ISubscriptionPlanConfigRepository
{
    Task<List<SubscriptionPlanConfig>> GetAllActiveAsync();
    Task<SubscriptionPlanConfig?> GetByPlanAsync(string plan);
    void Update(SubscriptionPlanConfig config);
}
