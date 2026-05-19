using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.Models;

namespace GuideMarket.Api.Repositories;

public interface ITourRepository : IRepository<Tour>
{
    Task<Tour?> GetByIdWithGuideAsync(Guid id);
    Task<Tour?> GetBySlugAsync(string slug);
    Task<bool> SlugExistsAsync(string slug);
    Task<(List<Tour> Items, long Total)> SearchAsync(TourSearchParams p, bool publicOnly);
    Task<(List<Tour> Items, long Total)> GetByGuideIdAsync(Guid guideId, int page, int size);
    Task<int> CountActiveByGuideAsync(Guid guideId);

    Task<List<TourAvailability>> GetAvailabilitiesAsync(Guid tourId, bool upcomingOnly = false);
    Task<TourAvailability?> GetAvailabilityByDateAsync(Guid tourId, DateOnly date);
    Task AddAvailabilityAsync(TourAvailability availability);
    void UpdateAvailability(TourAvailability availability);
    void DeleteAvailability(TourAvailability availability);
}
