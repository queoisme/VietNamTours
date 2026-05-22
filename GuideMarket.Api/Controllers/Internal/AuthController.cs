using System.Text.Json.Serialization;
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
    /// Supabase webhook: fires after email confirmation or OAuth sign-up.
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

        var record = payload.Record;

        // Extract full_name: check user_metadata first (email/password), then raw_user_meta_data (OAuth)
        var fullName =
            GetMetadataString(record.UserMetadata, "full_name") ??
            GetMetadataString(record.RawUserMetaData, "full_name") ??
            GetMetadataString(record.RawUserMetaData, "name") ??
            record.Email;

        // Extract avatar_url from Google OAuth metadata
        var avatarUrl =
            GetMetadataString(record.RawUserMetaData, "avatar_url") ??
            GetMetadataString(record.RawUserMetaData, "picture");

        await _authService.CreateUserFromWebhookAsync(record.Id, record.Email, fullName, avatarUrl);

        return Ok(ApiResponse<object>.Ok(null!, "User created"));
    }

    private static string? GetMetadataString(Dictionary<string, object?>? dict, string key)
    {
        if (dict is null) return null;
        return dict.TryGetValue(key, out var val) ? val?.ToString() : null;
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

    [JsonPropertyName("raw_user_meta_data")]
    public Dictionary<string, object?>? RawUserMetaData { get; set; }
}
