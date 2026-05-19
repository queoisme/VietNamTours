using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IBookingRepository : IRepository<Booking>
{
    Task<Booking?> GetByIdWithDetailsAsync(Guid id);
    Task<(List<Booking> Items, long Total)> GetByCustomerIdAsync(Guid customerId, string? status, int page, int size);
    Task<(List<Booking> Items, long Total)> GetByGuideIdAsync(Guid guideId, string? status, int page, int size);
    Task<Booking?> GetByPaymentTxnIdAsync(string txnId);
    Task AddConversationAsync(Conversation conversation);
}
