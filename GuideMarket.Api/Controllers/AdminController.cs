using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly IAdminService _admin;

    public AdminController(IAdminService admin) => _admin = admin;

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var userId = GetCurrentUserId();
        var stats  = await _admin.GetStatsAsync(userId);
        return Ok(ApiResponse<AdminStatsResponse>.Ok(stats));
    }

    [HttpGet("stats/revenue")]
    public async Task<IActionResult> GetRevenue(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
    {
        var userId  = GetCurrentUserId();
        var revenue = await _admin.GetRevenueAsync(userId, from, to);
        return Ok(ApiResponse<AdminRevenueResponse>.Ok(revenue));
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? role, [FromQuery] string? q,
        [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId      = GetCurrentUserId();
        var clampedSize = Math.Clamp(size, 1, 100);
        var (items, total) = await _admin.GetUsersAsync(userId, role, q, page, clampedSize);
        return Ok(ApiResponse<List<AdminUserResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page  = Math.Max(page, 1),
            Size  = clampedSize,
            Total = total,
        }));
    }

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUser(Guid id)
    {
        var userId = GetCurrentUserId();
        var user   = await _admin.GetUserByIdAsync(userId, id);
        return Ok(ApiResponse<AdminUserResponse>.Ok(user));
    }

    [HttpPut("users/{id:guid}/ban")]
    public async Task<IActionResult> BanUser(Guid id, [FromBody] BanUserRequest request)
    {
        var userId = GetCurrentUserId();
        await _admin.BanUserAsync(userId, id, request);
        var message = request.IsBanned ? "User has been banned" : "User has been unbanned";
        return Ok(ApiResponse<object?>.Ok(null, message));
    }

    [HttpGet("tours")]
    public async Task<IActionResult> GetTours(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId      = GetCurrentUserId();
        var clampedSize = Math.Clamp(size, 1, 100);
        var (items, total) = await _admin.GetToursAsync(userId, status, page, clampedSize);
        return Ok(ApiResponse<List<AdminTourResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page  = Math.Max(page, 1),
            Size  = clampedSize,
            Total = total,
        }));
    }

    [HttpPut("tours/{id:guid}/status")]
    public async Task<IActionResult> UpdateTourStatus(Guid id, [FromBody] UpdateTourStatusRequest request)
    {
        var userId = GetCurrentUserId();
        await _admin.UpdateTourStatusAsync(userId, id, request);
        return Ok(ApiResponse<object?>.Ok(null, "Tour status updated"));
    }

    [HttpGet("reports/export")]
    public async Task<IActionResult> ExportBookings(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? status)
    {
        var userId = GetCurrentUserId();
        var csv    = await _admin.ExportBookingsAsync(userId, from, to, status);
        var filename = $"bookings_{DateTime.UtcNow:yyyyMMdd}.csv";
        return File(csv, "text/csv", filename);
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
