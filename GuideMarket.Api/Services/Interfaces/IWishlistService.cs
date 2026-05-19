using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IWishlistService
{
    Task<(List<WishlistItemResponse> Items, long Total)> GetMyAsync(Guid customerId, int page, int size);
    Task AddAsync(Guid customerId, Guid tourId);
    Task RemoveAsync(Guid customerId, Guid tourId);
}
