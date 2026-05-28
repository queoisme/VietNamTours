using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingsController(IBookingService bookingService) => _bookingService = bookingService;

    [HttpPost("bookings")]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateBookingRequest request)
    {
        var userId  = GetCurrentUserId();
        var booking = await _bookingService.CreateAsync(userId, request);
        return StatusCode(201, ApiResponse<BookingDetailResponse>.Ok(booking, "Booking created"));
    }

    [HttpGet("bookings/my")]
    [Authorize]
    public async Task<IActionResult> GetMyBookings(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId         = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _bookingService.GetMyBookingsAsync(userId, status, page, clampedSize);
        return Ok(ApiResponse<List<BookingListItemResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page  = Math.Max(page, 1),
            Size  = clampedSize,
            Total = total,
        }));
    }

    [HttpGet("bookings/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var userId  = GetCurrentUserId();
        var booking = await _bookingService.GetByIdAsync(userId, id);
        return Ok(ApiResponse<BookingDetailResponse>.Ok(booking));
    }

    [HttpPost("bookings/{id:guid}/confirm")]
    [Authorize]
    public async Task<IActionResult> Confirm(Guid id)
    {
        var userId  = GetCurrentUserId();
        var booking = await _bookingService.ConfirmAsync(userId, id);
        return Ok(ApiResponse<BookingDetailResponse>.Ok(booking, "Booking confirmed"));
    }

    [HttpPost("bookings/{id:guid}/reject")]
    [Authorize]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectBookingRequest request)
    {
        var userId  = GetCurrentUserId();
        var booking = await _bookingService.RejectAsync(userId, id, request);
        return Ok(ApiResponse<BookingDetailResponse>.Ok(booking, "Booking rejected"));
    }

    [HttpPost("bookings/{id:guid}/complete")]
    [Authorize]
    public async Task<IActionResult> Complete(Guid id)
    {
        var userId  = GetCurrentUserId();
        var booking = await _bookingService.CompleteAsync(userId, id);
        return Ok(ApiResponse<BookingDetailResponse>.Ok(booking, "Booking completed"));
    }

    [HttpPost("bookings/{id:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelBookingRequest request)
    {
        var userId  = GetCurrentUserId();
        var booking = await _bookingService.CancelAsync(userId, id, request);
        return Ok(ApiResponse<BookingDetailResponse>.Ok(booking, "Booking cancelled"));
    }

    [HttpPost("bookings/{id:guid}/guide-cancel")]
    [Authorize]
    public async Task<IActionResult> GuideCancel(Guid id, [FromBody] CancelBookingRequest request)
    {
        var userId  = GetCurrentUserId();
        var booking = await _bookingService.GuideCancelAsync(userId, id, request);
        return Ok(ApiResponse<BookingDetailResponse>.Ok(booking, "Booking cancelled by guide"));
    }

    [HttpGet("guides/me/bookings")]
    [Authorize]
    public async Task<IActionResult> GetGuideBookings(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId         = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _bookingService.GetGuideBookingsAsync(userId, status, page, clampedSize);
        return Ok(ApiResponse<List<BookingListItemResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page  = Math.Max(page, 1),
            Size  = clampedSize,
            Total = total,
        }));
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
