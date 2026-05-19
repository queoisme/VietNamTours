using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Models;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class ToursController : ControllerBase
{
    private readonly ITourService _tourService;

    public ToursController(ITourService tourService) => _tourService = tourService;

    /// <summary>Tìm kiếm tour (public).</summary>
    [HttpGet("tours")]
    public async Task<IActionResult> Search([FromQuery] TourSearchParams p)
    {
        var (items, total) = await _tourService.SearchAsync(p);
        var size = Math.Clamp(p.Size, 1, 100);
        return Ok(ApiResponse<List<TourListItemResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(p.Page, 1),
            Size = size,
            Total = total,
        }));
    }

    /// <summary>Lấy chi tiết tour. Public trả active; guide owner xem được cả draft/inactive.</summary>
    [HttpGet("tours/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        // Lấy userId nếu có token (optional auth)
        Guid? requestingUserId = null;
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(sub, out var uid)) requestingUserId = uid;

        var tour = await _tourService.GetByIdAsync(id, requestingUserId);
        if (tour is null) return NotFound(ApiResponse<object>.Fail("Tour not found"));
        return Ok(ApiResponse<TourResponse>.Ok(tour));
    }

    /// <summary>Lấy danh sách slot còn trống (public).</summary>
    [HttpGet("tours/{id:guid}/availabilities")]
    public async Task<IActionResult> GetAvailabilities(Guid id)
    {
        var result = await _tourService.GetAvailabilitiesAsync(id, upcomingOnly: true);
        return Ok(ApiResponse<List<TourAvailabilityResponse>>.Ok(result));
    }

    /// <summary>Tạo tour mới (Guide only).</summary>
    [HttpPost("tours")]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateTourRequest request)
    {
        var userId = GetCurrentUserId();
        var tour = await _tourService.CreateAsync(userId, request);
        return StatusCode(201, ApiResponse<TourResponse>.Ok(tour, "Tour created"));
    }

    /// <summary>Cập nhật tour (Guide, chủ sở hữu).</summary>
    [HttpPut("tours/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTourRequest request)
    {
        var userId = GetCurrentUserId();
        var tour = await _tourService.UpdateAsync(userId, id, request);
        return Ok(ApiResponse<TourResponse>.Ok(tour, "Tour updated"));
    }

    /// <summary>Xóa mềm tour (Guide, chủ sở hữu).</summary>
    [HttpDelete("tours/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        await _tourService.DeleteAsync(userId, id);
        return Ok(ApiResponse<object>.Ok(null!, "Tour deleted"));
    }

    /// <summary>Cập nhật trạng thái tour (Guide, chủ sở hữu).</summary>
    [HttpPut("tours/{id:guid}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTourStatusRequest request)
    {
        if (!Enum.TryParse<TourStatus>(request.Status, true, out var status))
            return BadRequest(ApiResponse<object>.Fail("Invalid status"));

        var userId = GetCurrentUserId();
        var tour = await _tourService.UpdateStatusAsync(userId, id, status);
        return Ok(ApiResponse<TourResponse>.Ok(tour, "Tour status updated"));
    }

    /// <summary>Danh sách tour của guide đang đăng nhập.</summary>
    [HttpGet("guides/me/tours")]
    [Authorize]
    public async Task<IActionResult> GetMyTours([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId = GetCurrentUserId();
        var (items, total) = await _tourService.GetGuideToursAsync(userId, page, size);
        var clampedSize = Math.Clamp(size, 1, 100);
        return Ok(ApiResponse<List<TourListItemResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(page, 1),
            Size = clampedSize,
            Total = total,
        }));
    }

    // --- Availability management (Guide only) ---

    /// <summary>Thêm ngày khả dụng cho tour (Guide, chủ sở hữu).</summary>
    [HttpPost("tours/{id:guid}/availabilities")]
    [Authorize]
    public async Task<IActionResult> CreateAvailability(Guid id, [FromBody] CreateAvailabilityRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _tourService.CreateAvailabilityAsync(userId, id, request);
        return StatusCode(201, ApiResponse<TourAvailabilityResponse>.Ok(result, "Availability created"));
    }

    /// <summary>Cập nhật ngày khả dụng (Guide, chủ sở hữu).</summary>
    [HttpPut("tours/{id:guid}/availabilities/{date}")]
    [Authorize]
    public async Task<IActionResult> UpdateAvailability(Guid id, DateOnly date, [FromBody] UpdateAvailabilityRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _tourService.UpdateAvailabilityAsync(userId, id, date, request);
        return Ok(ApiResponse<TourAvailabilityResponse>.Ok(result, "Availability updated"));
    }

    /// <summary>Xóa ngày khả dụng (Guide, chủ sở hữu).</summary>
    [HttpDelete("tours/{id:guid}/availabilities/{date}")]
    [Authorize]
    public async Task<IActionResult> DeleteAvailability(Guid id, DateOnly date)
    {
        var userId = GetCurrentUserId();
        await _tourService.DeleteAvailabilityAsync(userId, id, date);
        return Ok(ApiResponse<object>.Ok(null!, "Availability deleted"));
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
