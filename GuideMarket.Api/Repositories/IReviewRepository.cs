using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IReviewRepository : IRepository<Review>
{
    Task<Review?> GetByBookingIdAsync(Guid bookingId);
    Task<Review?> GetByIdWithDetailsAsync(Guid id);
    Task<(List<Review> Items, long Total)> GetByTourIdAsync(Guid tourId, int page, int size);
    Task<(List<Review> Items, long Total)> GetByCustomerIdAsync(Guid customerId, int page, int size);
}
