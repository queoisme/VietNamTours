using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviews;

    public ReviewsController(IReviewService reviews) => _reviews = reviews;

    [HttpGet("tours/{tourId:guid}/reviews")]
    public async Task<IActionResult> GetByTour(
        Guid tourId, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _reviews.GetByTourIdAsync(tourId, page, clampedSize);
        return Ok(ApiResponse<List<ReviewResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(page, 1), Size = clampedSize, Total = total,
        }));
    }

    [HttpGet("customers/me/reviews")]
    [Authorize]
    public async Task<IActionResult> GetMine([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId         = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _reviews.GetMyReviewsAsync(userId, page, clampedSize);
        return Ok(ApiResponse<List<ReviewResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(page, 1), Size = clampedSize, Total = total,
        }));
    }

    [HttpPost("reviews")]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest request)
    {
        var userId = GetCurrentUserId();
        var review = await _reviews.CreateAsync(userId, request);
        return StatusCode(201, ApiResponse<ReviewResponse>.Ok(review, "Review created"));
    }

    [HttpPut("reviews/{id:guid}/reply")]
    [Authorize]
    public async Task<IActionResult> Reply(Guid id, [FromBody] ReplyReviewRequest request)
    {
        var userId = GetCurrentUserId();
        var review = await _reviews.ReplyAsync(userId, id, request);
        return Ok(ApiResponse<ReviewResponse>.Ok(review));
    }

    [HttpPut("admin/reviews/{id:guid}/visibility")]
    [Authorize]
    public async Task<IActionResult> ToggleVisibility(Guid id)
    {
        var userId = GetCurrentUserId();
        var review = await _reviews.ToggleVisibilityAsync(userId, id);
        return Ok(ApiResponse<ReviewResponse>.Ok(review));
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
