using System.Text.Json;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

/// <summary>
/// Endpoints dành cho Supabase Webhooks và internal service calls.
/// Bảo vệ bằng Service Role Key trong header Authorization.
/// </summary>
[ApiController]
[Route("api/v1/internal")]
public class InternalController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUnitOfWork _uow;
    private readonly IConfiguration _config;
    private readonly ILogger<InternalController> _logger;

    public InternalController(
        IAuthService authService,
        IUnitOfWork uow,
        IConfiguration config,
        ILogger<InternalController> logger)
    {
        _authService = authService;
        _uow = uow;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Supabase Webhook: email xác nhận → tạo user trong public.users.
    /// Payload: {"type":"INSERT","schema":"auth","table":"users","record":{...}}
    /// </summary>
    [HttpPost("auth/user-created")]
    public async Task<IActionResult> UserCreated([FromBody] JsonElement payload)
    {
        if (!ValidateServiceKey()) return Unauthorized(ApiResponse<object>.Fail("Unauthorized"));

        try
        {
            // Supabase Auth Webhook payload structure
            var record = payload.TryGetProperty("record", out var rec) ? rec : payload;

            if (!record.TryGetProperty("id", out var idProp) ||
                !Guid.TryParse(idProp.GetString(), out var userId))
            {
                _logger.LogWarning("user-created webhook: missing or invalid id");
                return BadRequest(ApiResponse<object>.Fail("Invalid payload: missing id"));
            }

            var email = record.TryGetProperty("email", out var emailProp)
                ? emailProp.GetString() ?? string.Empty : string.Empty;

            var fullName = string.Empty;
            if (record.TryGetProperty("raw_user_meta_data", out var meta) &&
                meta.TryGetProperty("full_name", out var nameProp))
                fullName = nameProp.GetString() ?? string.Empty;

            await _authService.CreateUserFromWebhookAsync(userId, email, fullName);
            _logger.LogInformation("Webhook: user {UserId} created/synced", userId);
            return Ok(ApiResponse<object>.Ok(null!, "User synced"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "user-created webhook error");
            return StatusCode(500, ApiResponse<object>.Fail("Internal error"));
        }
    }

    /// <summary>
    /// Sync role từ Supabase app_metadata → public.users.
    /// Body: {"userId": "uuid", "role": "guide"}
    /// </summary>
    [HttpPost("auth/sync-role")]
    public async Task<IActionResult> SyncRole([FromBody] SyncRoleRequest request)
    {
        if (!ValidateServiceKey()) return Unauthorized(ApiResponse<object>.Fail("Unauthorized"));

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            return BadRequest(ApiResponse<object>.Fail($"Invalid role: {request.Role}"));

        var user = await _uow.Users.GetByIdAsync(request.UserId);
        if (user is null)
        {
            _logger.LogWarning("sync-role: user {UserId} not found", request.UserId);
            return NotFound(ApiResponse<object>.Fail("User not found"));
        }

        user.Role = role;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Role synced: user {UserId} → {Role}", request.UserId, role);
        return Ok(ApiResponse<object>.Ok(null!, "Role synced"));
    }

    private bool ValidateServiceKey()
    {
        var serviceKey = _config["Supabase:ServiceRoleKey"];
        var authHeader = Request.Headers.Authorization.ToString();

        return !string.IsNullOrEmpty(serviceKey) &&
               authHeader.Equals($"Bearer {serviceKey}", StringComparison.Ordinal);
    }
}

public class SyncRoleRequest
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = default!;
}
