using GuideMarket.Api.DTOs.Requests;

namespace GuideMarket.Api.Services.Interfaces;

public interface IAnalyticsService
{
    void TrackSearch(TourSearchParams p, int resultCount, Guid? userId);
    void TrackPageView(string path, Guid? userId);
}
