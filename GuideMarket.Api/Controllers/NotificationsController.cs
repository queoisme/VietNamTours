using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications) =>
        _notifications = notifications;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId      = GetCurrentUserId();
        var clampedSize = Math.Clamp(size, 1, 100);
        var (items, total) = await _notifications.GetByUserIdAsync(userId, page, clampedSize);
        return Ok(ApiResponse<List<NotificationDto>>.Ok(items, meta: new PaginationMeta
        {
            Page  = Math.Max(page, 1),
            Size  = clampedSize,
            Total = total,
        }));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetCurrentUserId();
        var count  = await _notifications.GetUnreadCountAsync(userId);
        return Ok(ApiResponse<object>.Ok(new { count }));
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = GetCurrentUserId();
        await _notifications.MarkReadAsync(userId, id);
        return Ok(ApiResponse<object?>.Ok(null, "Marked as read"));
    }

    [HttpPut("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = GetCurrentUserId();
        await _notifications.MarkAllReadAsync(userId);
        return Ok(ApiResponse<object?>.Ok(null, "All notifications marked as read"));
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
