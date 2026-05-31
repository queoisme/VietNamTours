using GuideMarket.Api.Data;
using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.Models;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GuideMarket.Api.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AnalyticsService(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public void TrackSearch(TourSearchParams p, int resultCount, Guid? userId)
        => _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.SearchEvents.Add(new SearchEvent
                {
                    Id           = Guid.NewGuid(),
                    Query        = string.IsNullOrWhiteSpace(p.Q) ? null : p.Q.Trim(),
                    Category     = p.Category,
                    LocationCity = p.City,
                    MinPrice     = p.MinPrice,
                    MaxPrice     = p.MaxPrice,
                    ResultCount  = resultCount,
                    UserId       = userId,
                    CreatedAt    = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync();
            }
            catch { /* analytics must never affect main request */ }
        });

    public void TrackPageView(string path, Guid? userId)
        => _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.PageViews.Add(new PageView
                {
                    Id        = Guid.NewGuid(),
                    Path      = path,
                    UserId    = userId,
                    CreatedAt = DateTimeOffset.UtcNow,
                });
                await db.SaveChangesAsync();
            }
            catch { /* analytics must never affect main request */ }
        });
}
