using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Authorize]
public class GuideApplicationsController : ControllerBase
{
    private readonly IGuideApplicationService _appService;

    public GuideApplicationsController(IGuideApplicationService appService) => _appService = appService;

    /// <summary>Customer nộp đơn đăng ký trở thành guide.</summary>
    [HttpPost("api/v1/guide-applications")]
    public async Task<IActionResult> Submit([FromBody] CreateGuideApplicationRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _appService.SubmitAsync(userId, request);
        return StatusCode(201, ApiResponse<GuideApplicationResponse>.Ok(result, "Application submitted"));
    }

    /// <summary>Customer xem đơn gần nhất đã nộp (null nếu chưa nộp).</summary>
    [HttpGet("api/v1/guide-applications/my")]
    public async Task<IActionResult> GetMy()
    {
        var userId = GetCurrentUserId();
        var result = await _appService.GetMyLatestApplicationAsync(userId);
        return Ok(ApiResponse<GuideApplicationResponse?>.Ok(result));
    }

    // --- Admin endpoints ---

    /// <summary>Admin xem danh sách tất cả đơn.</summary>
    [HttpGet("api/v1/admin/guide-applications")]
    public async Task<IActionResult> GetAll([FromQuery] GuideApplicationListParams p)
    {
        var adminId = GetCurrentUserId();
        var (items, total) = await _appService.GetAllAsync(adminId, p);
        var size = Math.Clamp(p.Size, 1, 100);
        return Ok(ApiResponse<List<GuideApplicationResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(p.Page, 1),
            Size = size,
            Total = total,
        }));
    }

    /// <summary>Admin xem chi tiết một đơn.</summary>
    [HttpGet("api/v1/admin/guide-applications/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var adminId = GetCurrentUserId();
        var result = await _appService.GetByIdAsync(adminId, id);
        if (result is null) return NotFound(ApiResponse<object>.Fail("Application not found"));
        return Ok(ApiResponse<GuideApplicationResponse>.Ok(result));
    }

    /// <summary>Admin duyệt đơn — tạo guide profile + nâng role.</summary>
    [HttpPost("api/v1/admin/guide-applications/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id)
    {
        var adminId = GetCurrentUserId();
        var result = await _appService.ApproveAsync(adminId, id);
        return Ok(ApiResponse<GuideApplicationResponse>.Ok(result, "Application approved"));
    }

    /// <summary>Admin từ chối đơn.</summary>
    [HttpPost("api/v1/admin/guide-applications/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectApplicationRequest request)
    {
        var adminId = GetCurrentUserId();
        var result = await _appService.RejectAsync(adminId, id, request);
        return Ok(ApiResponse<GuideApplicationResponse>.Ok(result, "Application rejected"));
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
