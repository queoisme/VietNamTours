using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IBoostRepository : IRepository<Boost>
{
    Task<Boost?> GetByPaymentTxnIdAsync(string txnId);
    Task<(List<Boost> Items, long Total)> GetByGuideIdAsync(Guid guideId, int page, int size);
}
