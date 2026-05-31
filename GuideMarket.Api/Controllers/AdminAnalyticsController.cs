using GuideMarket.Api.Data;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1/admin/insights")]
[Authorize]
public class AdminAnalyticsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IUnitOfWork _uow;

    public AdminAnalyticsController(AppDbContext db, IUnitOfWork uow)
    {
        _db  = db;
        _uow = uow;
    }

    /// <summary>GET /admin/insights/searches?from=&amp;to=</summary>
    [HttpGet("searches")]
    public async Task<IActionResult> GetSearchAnalytics(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        await RequireAdminAsync();

        var fromOffset = ToOffsetStart(from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
        var toOffset   = ToOffsetEnd(to   ?? DateOnly.FromDateTime(DateTime.UtcNow));

        var query = _db.SearchEvents.AsNoTracking()
            .Where(e => e.CreatedAt >= fromOffset && e.CreatedAt <= toOffset);

        var totalSearches = await query.CountAsync();

        var dailyCounts = await query
            .GroupBy(e => e.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var topCategories = await query
            .Where(e => e.Category != null)
            .GroupBy(e => e.Category!)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync();

        var topCities = await query
            .Where(e => e.LocationCity != null)
            .GroupBy(e => e.LocationCity!)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(8)
            .ToListAsync();

        var topKeywords = await query
            .Where(e => e.Query != null)
            .GroupBy(e => e.Query!)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync();

        var priceEvents = await query
            .Where(e => e.MinPrice != null)
            .Select(e => e.MinPrice!.Value)
            .ToListAsync();

        var priceRangeCounts = new List<LabelCountDto>
        {
            new("Dưới 200k",      priceEvents.Count(p => p < 200_000)),
            new("200k – 500k",    priceEvents.Count(p => p >= 200_000  && p < 500_000)),
            new("500k – 1 triệu", priceEvents.Count(p => p >= 500_000  && p < 1_000_000)),
            new("1 – 2 triệu",    priceEvents.Count(p => p >= 1_000_000 && p < 2_000_000)),
            new("Trên 2 triệu",   priceEvents.Count(p => p >= 2_000_000)),
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

    /// <summary>GET /admin/insights/page-views?from=&amp;to=</summary>
    [HttpGet("page-views")]
    public async Task<IActionResult> GetPageViewAnalytics(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        await RequireAdminAsync();

        var fromOffset = ToOffsetStart(from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)));
        var toOffset   = ToOffsetEnd(to   ?? DateOnly.FromDateTime(DateTime.UtcNow));

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

    private async Task RequireAdminAsync()
    {
        var sub = User.FindFirst("sub")?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Invalid token");

        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");

        if (user.Role != UserRole.admin)
            throw new ForbiddenAccessException("Only admins can access this");
    }

    private static DateTimeOffset ToOffsetStart(DateOnly d)
        => new(d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    private static DateTimeOffset ToOffsetEnd(DateOnly d)
        => new(d.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
}
