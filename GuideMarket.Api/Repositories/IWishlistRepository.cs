using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface IWishlistRepository : IRepository<Wishlist>
{
    Task<Wishlist?> GetByCustomerAndTourAsync(Guid customerId, Guid tourId);
    Task<(List<Wishlist> Items, long Total)> GetByCustomerIdAsync(Guid customerId, int page, int size);
}
