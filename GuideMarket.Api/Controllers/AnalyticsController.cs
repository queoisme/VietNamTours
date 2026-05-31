using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsController(IAnalyticsService analytics)
        => _analytics = analytics;

    [HttpPost("page-view")]
    [AllowAnonymous]
    public IActionResult TrackPageView([FromBody] TrackPageViewRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Path))
            return Ok();

        var userId = User.FindFirst("sub")?.Value is { } s && Guid.TryParse(s, out var g)
            ? g : (Guid?)null;

        _analytics.TrackPageView(req.Path.Trim(), userId);
        return Ok();
    }
}

public record TrackPageViewRequest(string Path);
