using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Models;

namespace GuideMarket.Api.Services.Interfaces;

public interface ITourService
{
    Task<(List<TourListItemResponse> Items, long Total)> SearchAsync(TourSearchParams p);
    Task<TourResponse?> GetByIdAsync(Guid id, Guid? requestingUserId = null);
    Task<TourResponse> CreateAsync(Guid userId, CreateTourRequest request);
    Task<TourResponse> UpdateAsync(Guid userId, Guid tourId, UpdateTourRequest request);
    Task DeleteAsync(Guid userId, Guid tourId);
    Task<TourResponse> UpdateStatusAsync(Guid userId, Guid tourId, TourStatus status);
    Task<(List<TourListItemResponse> Items, long Total)> GetGuideToursAsync(Guid userId, int page, int size);

    Task<List<TourAvailabilityResponse>> GetAvailabilitiesAsync(Guid tourId, bool upcomingOnly);
    Task<TourAvailabilityResponse> CreateAvailabilityAsync(Guid userId, Guid tourId, CreateAvailabilityRequest request);
    Task<TourAvailabilityResponse> UpdateAvailabilityAsync(Guid userId, Guid tourId, DateOnly date, UpdateAvailabilityRequest request);
    Task DeleteAvailabilityAsync(Guid userId, Guid tourId, DateOnly date);
}
