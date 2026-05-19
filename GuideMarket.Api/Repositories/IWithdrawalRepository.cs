using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IWithdrawalRepository : IRepository<Withdrawal>
{
    Task<Withdrawal?> GetByIdWithGuideAsync(Guid id);
    Task<(List<Withdrawal> Items, long Total)> GetByGuideIdAsync(Guid guideId, int page, int size);
    Task<(List<Withdrawal> Items, long Total)> GetAllAsync(string? status, int page, int size);
}
