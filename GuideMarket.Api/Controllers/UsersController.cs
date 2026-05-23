using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Infrastructure;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly SupabaseStorageClient _storage;

    public UsersController(IUserService userService, IAuthService authService, SupabaseStorageClient storage)
    {
        _userService = userService;
        _authService = authService;
        _storage = storage;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMe()
    {
        var userId = GetCurrentUserId();
        var user = await _userService.GetByIdAsync(userId);
        if (user is null)
        {
            // OAuth user chưa có record (webhook chưa kịp fire) → tự tạo
            try
            {
                var loginResult = await _authService.HandleSocialLoginAsync(userId);
                return Ok(ApiResponse<UserResponse>.Ok(loginResult.User));
            }
            catch
            {
                return NotFound(ApiResponse<object>.Fail("User not found"));
            }
        }
        return Ok(ApiResponse<UserResponse>.Ok(user));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateUserRequest request)
    {
        var userId = GetCurrentUserId();
        var user = await _userService.UpdateAsync(userId, request);
        return Ok(ApiResponse<UserResponse>.Ok(user, "Profile updated"));
    }

    [HttpPut("me/avatar")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateAvatar([FromForm] AvatarUploadRequest request)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("File is required"));

        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(ApiResponse<object>.Fail("File size must not exceed 5 MB"));

        var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(ApiResponse<object>.Fail("Only JPEG, PNG, and WebP are allowed"));

        var userId = GetCurrentUserId();

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var path = $"{userId}/avatar{ext}";

        using var stream = file.OpenReadStream();
        var avatarUrl = await _storage.UploadPublicAsync("avatars", path, stream, file.ContentType);

        var user = await _userService.UpdateAvatarAsync(userId, avatarUrl);
        return Ok(ApiResponse<UserResponse>.Ok(user, "Avatar updated"));
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
