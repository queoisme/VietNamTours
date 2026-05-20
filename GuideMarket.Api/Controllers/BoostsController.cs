using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class BoostsController : ControllerBase
{
    private readonly IBoostService       _boosts;
    private readonly ISubscriptionService _subscriptions;

    public BoostsController(IBoostService boosts, ISubscriptionService subscriptions)
    {
        _boosts        = boosts;
        _subscriptions = subscriptions;
    }

    // ── Boosts ──────────────────────────────────────────────────────────────

    [HttpGet("boosts/plans")]
    public IActionResult GetBoostPlans() =>
        Ok(ApiResponse<List<BoostPlanInfo>>.Ok(_boosts.GetPlans()));

    [HttpPost("boosts")]
    [Authorize]
    public async Task<IActionResult> CreateBoost([FromBody] CreateBoostRequest request)
    {
        var userId   = GetCurrentUserId();
        var ip       = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var payment  = await _boosts.CreateAsync(userId, request, ip);
        return StatusCode(201, ApiResponse<MomoPaymentResponse>.Ok(payment));
    }

    [HttpGet("guides/me/boosts")]
    [Authorize]
    public async Task<IActionResult> GetMyBoosts([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId         = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _boosts.GetMyBoostsAsync(userId, page, clampedSize);
        return Ok(ApiResponse<List<BoostResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(page, 1), Size = clampedSize, Total = total,
        }));
    }

    // ── Subscriptions ────────────────────────────────────────────────────────

    [HttpGet("subscriptions/plans")]
    public IActionResult GetSubscriptionPlans() =>
        Ok(ApiResponse<List<SubscriptionPlanInfo>>.Ok(_subscriptions.GetPlans()));

    [HttpPost("subscriptions")]
    [Authorize]
    public async Task<IActionResult> Subscribe([FromBody] CreateSubscriptionRequest request)
    {
        var userId  = GetCurrentUserId();
        var ip      = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var payment = await _subscriptions.CreateAsync(userId, request, ip);
        return StatusCode(201, ApiResponse<MomoPaymentResponse>.Ok(payment));
    }

    [HttpGet("guides/me/subscription")]
    [Authorize]
    public async Task<IActionResult> GetMySubscription()
    {
        var userId = GetCurrentUserId();
        var sub    = await _subscriptions.GetMySubscriptionAsync(userId);
        return Ok(ApiResponse<SubscriptionResponse?>.Ok(sub));
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Invalid token subject");
        return userId;
    }
}
