using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1/wishlists")]
[Authorize]
public class WishlistsController : ControllerBase
{
    private readonly IWishlistService _wishlists;

    public WishlistsController(IWishlistService wishlists) => _wishlists = wishlists;

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId         = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _wishlists.GetMyAsync(userId, page, clampedSize);
        return Ok(ApiResponse<List<WishlistItemResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(page, 1), Size = clampedSize, Total = total,
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] WishlistAddRequest request)
    {
        var userId = GetCurrentUserId();
        await _wishlists.AddAsync(userId, request.TourId);
        return StatusCode(201, ApiResponse<object?>.Ok(null, "Added to wishlist"));
    }

    [HttpDelete("{tourId:guid}")]
    public async Task<IActionResult> Remove(Guid tourId)
    {
        var userId = GetCurrentUserId();
        await _wishlists.RemoveAsync(userId, tourId);
        return Ok(ApiResponse<object?>.Ok(null, "Removed from wishlist"));
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

public class WishlistAddRequest
{
    public Guid TourId { get; set; }
}
