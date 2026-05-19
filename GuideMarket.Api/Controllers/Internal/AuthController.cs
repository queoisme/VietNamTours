using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers.Internal;

[ApiController]
[Route("internal/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _config;

    public AuthController(IAuthService authService, IConfiguration config)
    {
        _authService = authService;
        _config = config;
    }

    /// <summary>
    /// Supabase webhook: fires after email confirmation.
    /// Protected by service role key in Authorization header.
    /// </summary>
    [HttpPost("user-created")]
    public async Task<IActionResult> UserCreated([FromBody] SupabaseWebhookPayload payload)
    {
        var serviceKey = _config["Supabase:ServiceRoleKey"];
        var authHeader = Request.Headers.Authorization.ToString();
        if (string.IsNullOrEmpty(serviceKey) || authHeader != $"Bearer {serviceKey}")
            return Unauthorized();

        if (payload.Record is null)
            return BadRequest(ApiResponse<object>.Fail("Missing record"));

        await _authService.CreateUserFromWebhookAsync(
            payload.Record.Id,
            payload.Record.Email,
            payload.Record.UserMetadata?.TryGetValue("full_name", out var name) == true
                ? name?.ToString() ?? payload.Record.Email
                : payload.Record.Email
        );

        return Ok(ApiResponse<object>.Ok(null!, "User created"));
    }
}

public class SupabaseWebhookPayload
{
    public string Type { get; set; } = default!;
    public string Table { get; set; } = default!;
    public SupabaseAuthRecord? Record { get; set; }
}

public class SupabaseAuthRecord
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public Dictionary<string, object?>? UserMetadata { get; set; }
}
