using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1/conversations")]
[Authorize]
public class ConversationsController : ControllerBase
{
    private readonly IConversationService _conversations;

    public ConversationsController(IConversationService conversations) => _conversations = conversations;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId         = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _conversations.GetConversationsAsync(userId, page, clampedSize);
        return Ok(ApiResponse<List<ConversationListItemResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(page, 1), Size = clampedSize, Total = total,
        }));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var userId = GetCurrentUserId();
        var conv   = await _conversations.GetConversationAsync(userId, id);
        return Ok(ApiResponse<ConversationListItemResponse>.Ok(conv));
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid id, [FromQuery] DateTimeOffset? before, [FromQuery] int size = 50)
    {
        var userId         = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _conversations.GetMessagesAsync(userId, id, before, clampedSize);
        return Ok(ApiResponse<List<MessageResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = 1, Size = clampedSize, Total = total,
        }));
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> Send(Guid id, [FromBody] SendMessageRequest request)
    {
        var userId  = GetCurrentUserId();
        var message = await _conversations.SendMessageAsync(userId, id, request);
        return StatusCode(201, ApiResponse<MessageResponse>.Ok(message));
    }

    [HttpPost("by-booking/{bookingId:guid}")]
    public async Task<IActionResult> GetOrCreateByBooking(Guid bookingId)
    {
        var userId = GetCurrentUserId();
        var conv   = await _conversations.GetOrCreateByBookingAsync(userId, bookingId);
        return Ok(ApiResponse<ConversationListItemResponse>.Ok(conv));
    }

    [HttpPost("by-tour/{tourId:guid}")]
    [Authorize(Roles = "customer")]
    public async Task<IActionResult> GetOrCreateByTour(Guid tourId)
    {
        var userId = GetCurrentUserId();
        var conv   = await _conversations.GetOrCreateByTourAsync(userId, tourId);
        return StatusCode(201, ApiResponse<ConversationListItemResponse>.Ok(conv));
    }

    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var userId = GetCurrentUserId();
        await _conversations.MarkReadAsync(userId, id);
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
