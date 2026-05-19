using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1/guides")]
public class GuidesController : ControllerBase
{
    private readonly IGuideProfileService _profileService;

    public GuidesController(IGuideProfileService profileService) => _profileService = profileService;

    /// <summary>Xem trang profile công khai của một guide.</summary>
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetPublicProfile(Guid userId)
    {
        var profile = await _profileService.GetPublicProfileAsync(userId);
        if (profile is null) return NotFound(ApiResponse<object>.Fail("Guide profile not found"));
        return Ok(ApiResponse<GuidePublicResponse>.Ok(profile));
    }

    /// <summary>Guide cập nhật profile của mình.</summary>
    [HttpPut("me/profile")]
    [Authorize]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateGuideProfileRequest request)
    {
        var userId = GetCurrentUserId();
        var profile = await _profileService.UpdateProfileAsync(userId, request);
        return Ok(ApiResponse<GuideProfileResponse>.Ok(profile, "Profile updated"));
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
