using GuideMarket.Api.Data;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1/admin/analytics")]
[Authorize]
public class AdminAnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminAnalyticsController(AppDbContext db) => _db = db;

    /// <summary>GET /admin/analytics/searches?from=&amp;to=</summary>
    [HttpGet("searches")]
    public async Task<IActionResult> GetSearchAnalytics(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        RequireAdmin();

        var fromDto = (from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDto = (to ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var fromOffset = new DateTimeOffset(fromDto);
        var toOffset   = new DateTimeOffset(toDto);

        var query = _db.SearchEvents.AsNoTracking()
            .Where(e => e.CreatedAt >= fromOffset && e.CreatedAt <= toOffset);

        var totalSearches = await query.CountAsync();

        // Daily counts
        var dailyCounts = await query
            .GroupBy(e => e.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        // Top categories (top 8)
        var topCategories = await query
            .Where(e => e.Category != null)
            .GroupBy(e => e.Category!)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync();

        // Top cities (top 8)
        var topCities = await query
            .Where(e => e.LocationCity != null)
            .GroupBy(e => e.LocationCity!)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync();

        // Top keywords (top 10)
        var topKeywords = await query
            .Where(e => e.Query != null)
            .GroupBy(e => e.Query!)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        // Price range distribution
        var priceEvents = await query
            .Where(e => e.MinPrice != null)
            .Select(e => e.MinPrice!.Value)
            .ToListAsync();

        var priceRangeCounts = new List<LabelCountDto>
        {
            new("Dưới 200k",     priceEvents.Count(p => p < 200_000)),
            new("200k – 500k",   priceEvents.Count(p => p >= 200_000 && p < 500_000)),
            new("500k – 1 triệu", priceEvents.Count(p => p >= 500_000 && p < 1_000_000)),
            new("1 – 2 triệu",   priceEvents.Count(p => p >= 1_000_000 && p < 2_000_000)),
            new("Trên 2 triệu",  priceEvents.Count(p => p >= 2_000_000)),
        }.Where(x => x.Count > 0).ToList();

        return Ok(ApiResponse<AdminSearchAnalyticsResponse>.Ok(new AdminSearchAnalyticsResponse
        {
            TotalSearches    = totalSearches,
            DailyCounts      = dailyCounts.Select(x => new DailyCountDto(x.Date.ToString("yyyy-MM-dd"), x.Count)).ToList(),
            TopCategories    = topCategories.Select(x => new LabelCountDto(x.Label, x.Count)).ToList(),
            TopCities        = topCities.Select(x => new LabelCountDto(x.Label, x.Count)).ToList(),
            TopKeywords      = topKeywords.Select(x => new LabelCountDto(x.Label, x.Count)).ToList(),
            PriceRangeCounts = priceRangeCounts,
        }));
    }

    /// <summary>GET /admin/analytics/page-views?from=&amp;to=</summary>
    [HttpGet("page-views")]
    public async Task<IActionResult> GetPageViewAnalytics(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        RequireAdmin();

        var fromDto = (from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)))
            .ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toDto = (to ?? DateOnly.FromDateTime(DateTime.UtcNow))
            .ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var fromOffset = new DateTimeOffset(fromDto);
        var toOffset   = new DateTimeOffset(toDto);

        var query = _db.PageViews.AsNoTracking()
            .Where(p => p.CreatedAt >= fromOffset && p.CreatedAt <= toOffset);

        var totalViews = await query.CountAsync();

        var dailyCounts = await query
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var topPages = await query
            .GroupBy(p => p.Path)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        return Ok(ApiResponse<AdminPageViewAnalyticsResponse>.Ok(new AdminPageViewAnalyticsResponse
        {
            TotalViews  = totalViews,
            DailyCounts = dailyCounts.Select(x => new DailyCountDto(x.Date.ToString("yyyy-MM-dd"), x.Count)).ToList(),
            TopPages    = topPages.Select(x => new LabelCountDto(x.Label, x.Count)).ToList(),
        }));
    }

    private void RequireAdmin()
    {
        var role = User.FindFirst("user_role")?.Value
                ?? User.FindFirst("role")?.Value;
        if (role != "admin")
            throw new UnauthorizedAccessException("Admin access required");
    }
}
