using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface ISubscriptionRepository : IRepository<Subscription>
{
    Task<Subscription?> GetByPaymentTxnIdAsync(string txnId);
    Task<Subscription?> GetActiveByGuideIdAsync(Guid guideId);
}
