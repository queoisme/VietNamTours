using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class HomeController : ControllerBase
{
    private readonly IHomeCategoryService _homeCategories;

    public HomeController(IHomeCategoryService homeCategories)
    {
        _homeCategories = homeCategories;
    }

    /// <summary>Public: trả về danh mục đang hiển thị, sắp xếp theo sort_order (cached 5 phút).</summary>
    [HttpGet("home/categories")]
    public async Task<IActionResult> GetHomeCategories()
    {
        var result = await _homeCategories.GetVisibleAsync();
        return Ok(ApiResponse<List<HomeCategoryResponse>>.Ok(result));
    }

    /// <summary>Admin: danh sách tất cả danh mục (kể cả ẩn).</summary>
    [HttpGet("admin/home-categories")]
    [Authorize]
    public async Task<IActionResult> AdminGetAll()
    {
        var result = await _homeCategories.GetAllAsync(GetCurrentUserId());
        return Ok(ApiResponse<List<HomeCategoryResponse>>.Ok(result));
    }

    /// <summary>Admin: tạo danh mục mới.</summary>
    [HttpPost("admin/home-categories")]
    [Authorize]
    public async Task<IActionResult> AdminCreate([FromBody] CreateHomeCategoryRequest request)
    {
        var result = await _homeCategories.CreateAsync(GetCurrentUserId(), request);
        return StatusCode(201, ApiResponse<HomeCategoryResponse>.Ok(result, "Đã tạo danh mục mới"));
    }

    /// <summary>Admin: cập nhật danh mục (patch-style — chỉ ghi đè field không null).</summary>
    [HttpPut("admin/home-categories/{id:int}")]
    [Authorize]
    public async Task<IActionResult> AdminUpdate(int id, [FromBody] UpdateHomeCategoryRequest request)
    {
        var result = await _homeCategories.UpdateAsync(GetCurrentUserId(), id, request);
        return Ok(ApiResponse<HomeCategoryResponse>.Ok(result, "Đã cập nhật danh mục"));
    }

    /// <summary>Admin: xóa danh mục.</summary>
    [HttpDelete("admin/home-categories/{id:int}")]
    [Authorize]
    public async Task<IActionResult> AdminDelete(int id)
    {
        await _homeCategories.DeleteAsync(GetCurrentUserId(), id);
        return Ok(ApiResponse<object?>.Ok(null, "Đã xóa danh mục"));
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
