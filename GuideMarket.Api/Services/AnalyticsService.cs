using GuideMarket.Api.Data;
using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.Models;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GuideMarket.Api.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(IServiceScopeFactory scopeFactory, ILogger<AnalyticsService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Analytics] TrackSearch failed");
            }
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
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Analytics] TrackPageView failed for path={Path}", path);
            }
        });
}
