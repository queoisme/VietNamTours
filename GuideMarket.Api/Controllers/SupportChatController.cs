using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Authorize]
public class SupportChatController : ControllerBase
{
    private readonly ISupportChatService _support;

    public SupportChatController(ISupportChatService support) => _support = support;

    // --- User endpoints ---

    [HttpPost("api/v1/support-chat")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateSupportTicketRequest request)
    {
        var userId = GetCurrentUserId();
        var ticket = await _support.CreateTicketAsync(userId, request);
        return StatusCode(201, ApiResponse<SupportTicketResponse>.Ok(ticket));
    }

    [HttpGet("api/v1/support-chat")]
    public async Task<IActionResult> GetMyTickets([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId         = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _support.GetMyTicketsAsync(userId, page, clampedSize);
        return Ok(ApiResponse<List<SupportTicketResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(page, 1), Size = clampedSize, Total = total,
        }));
    }

    [HttpGet("api/v1/support-chat/{id:guid}")]
    public async Task<IActionResult> GetTicket(Guid id)
    {
        var userId = GetCurrentUserId();
        var ticket = await _support.GetTicketAsync(userId, id, isAdmin: false);
        return Ok(ApiResponse<SupportTicketResponse>.Ok(ticket));
    }

    [HttpGet("api/v1/support-chat/{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid id, [FromQuery] DateTimeOffset? before, [FromQuery] int size = 50)
    {
        var userId         = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _support.GetMessagesAsync(userId, id, isAdmin: false, before, clampedSize);
        return Ok(ApiResponse<List<SupportMessageResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = 1, Size = clampedSize, Total = total,
        }));
    }

    [HttpPost("api/v1/support-chat/{id:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid id, [FromBody] SendSupportMessageRequest request)
    {
        var userId = GetCurrentUserId();
        var msg    = await _support.SendMessageAsync(userId, id, request, isAdmin: false);
        return StatusCode(201, ApiResponse<SupportMessageResponse>.Ok(msg));
    }

    [HttpPut("api/v1/support-chat/{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = GetCurrentUserId();
        await _support.MarkReadAsync(userId, id, isAdmin: false);
        return Ok(ApiResponse<object?>.Ok(null, "Marked as read"));
    }

    // --- Admin endpoints ---

    [HttpGet("api/v1/admin/support-chat")]
    public async Task<IActionResult> GetAllTickets(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _support.GetAllTicketsAsync(page, clampedSize, status);
        return Ok(ApiResponse<List<SupportTicketResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(page, 1), Size = clampedSize, Total = total,
        }));
    }

    [HttpGet("api/v1/admin/support-chat/{id:guid}/messages")]

    public async Task<IActionResult> AdminGetMessages(
        Guid id, [FromQuery] DateTimeOffset? before, [FromQuery] int size = 50)
    {
        var adminId        = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _support.GetMessagesAsync(adminId, id, isAdmin: true, before, clampedSize);
        return Ok(ApiResponse<List<SupportMessageResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = 1, Size = clampedSize, Total = total,
        }));
    }

    [HttpPost("api/v1/admin/support-chat/{id:guid}/messages")]

    public async Task<IActionResult> AdminSendMessage(Guid id, [FromBody] SendSupportMessageRequest request)
    {
        var adminId = GetCurrentUserId();
        var msg     = await _support.SendMessageAsync(adminId, id, request, isAdmin: true);
        return StatusCode(201, ApiResponse<SupportMessageResponse>.Ok(msg));
    }

    [HttpPut("api/v1/admin/support-chat/{id:guid}/status")]

    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateSupportStatusRequest request)
    {
        var adminId = GetCurrentUserId();
        var ticket  = await _support.UpdateStatusAsync(adminId, id, request.Status);
        return Ok(ApiResponse<SupportTicketResponse>.Ok(ticket));
    }

    [HttpPut("api/v1/admin/support-chat/{id:guid}/read")]

    public async Task<IActionResult> AdminMarkRead(Guid id)
    {
        var adminId = GetCurrentUserId();
        await _support.MarkReadAsync(adminId, id, isAdmin: true);
        return Ok(ApiResponse<object?>.Ok(null, "Marked as read"));
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
