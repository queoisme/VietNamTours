using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class WishlistService : IWishlistService
{
    private readonly IUnitOfWork _uow;

    public WishlistService(IUnitOfWork uow) => _uow = uow;

    public async Task<(List<WishlistItemResponse> Items, long Total)> GetMyAsync(Guid customerId, int page, int size)
    {
        var (items, total) = await _uow.Wishlists.GetByCustomerIdAsync(customerId, page, size);
        return (items.Select(Map).ToList(), total);
    }

    public async Task AddAsync(Guid customerId, Guid tourId)
    {
        var tour = await _uow.Tours.GetByIdAsync(tourId)
            ?? throw new KeyNotFoundException("Tour not found");

        if (tour.DeletedAt != null || tour.Status != TourStatus.active)
            throw new KeyNotFoundException("Tour not found");

        var existing = await _uow.Wishlists.GetByCustomerAndTourAsync(customerId, tourId);
        if (existing != null)
            throw new InvalidOperationException("Tour already in wishlist");

        await _uow.Wishlists.AddAsync(new Wishlist
        {
            Id         = Guid.NewGuid(),
            CustomerId = customerId,
            TourId     = tourId,
            CreatedAt  = DateTimeOffset.UtcNow,
        });
        await _uow.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid customerId, Guid tourId)
    {
        var item = await _uow.Wishlists.GetByCustomerAndTourAsync(customerId, tourId)
            ?? throw new KeyNotFoundException("Wishlist item not found");

        _uow.Wishlists.Delete(item);
        await _uow.SaveChangesAsync();
    }

    private static WishlistItemResponse Map(Wishlist w) => new(
        w.Id, w.TourId, w.Tour.Title, w.Tour.CoverImageUrl,
        w.Tour.LocationCity, w.Tour.PricePerPerson,
        w.Tour.AvgRating, w.Tour.TotalReviews, w.CreatedAt);
}
