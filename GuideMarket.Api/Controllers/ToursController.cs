using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Infrastructure;
using GuideMarket.Api.Models;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class ToursController : ControllerBase
{
    private readonly ITourService _tourService;
    private readonly SupabaseStorageClient _storage;
    private readonly IAnalyticsService _analytics;

    private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];
    private const long MaxImageSize = 10 * 1024 * 1024; // 10 MB

    public ToursController(ITourService tourService, SupabaseStorageClient storage, IAnalyticsService analytics)
    {
        _tourService = tourService;
        _storage = storage;
        _analytics = analytics;
    }

    /// <summary>Tìm kiếm tour (public).</summary>
    [HttpGet("tours")]
    public async Task<IActionResult> Search([FromQuery] TourSearchParams p)
    {
        var (items, total) = await _tourService.SearchAsync(p);
        var size = Math.Clamp(p.Size, 1, 100);

        var userId = User.FindFirst("sub")?.Value is { } s && Guid.TryParse(s, out var g)
            ? g : (Guid?)null;
        _analytics.TrackSearch(p, (int)total, userId);

        return Ok(ApiResponse<List<TourListItemResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(p.Page, 1),
            Size = size,
            Total = total,
        }));
    }

    /// <summary>Lấy chi tiết tour. Public trả active; guide owner xem được cả draft/inactive.</summary>
    [HttpGet("tours/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        // Lấy userId nếu có token (optional auth)
        Guid? requestingUserId = null;
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (Guid.TryParse(sub, out var uid)) requestingUserId = uid;

        var tour = await _tourService.GetByIdAsync(id, requestingUserId);
        if (tour is null) return NotFound(ApiResponse<object>.Fail("Tour not found"));
        return Ok(ApiResponse<TourResponse>.Ok(tour));
    }

    /// <summary>Lấy danh sách slot còn trống (public).</summary>
    [HttpGet("tours/{id:guid}/availabilities")]
    public async Task<IActionResult> GetAvailabilities(Guid id)
    {
        var result = await _tourService.GetAvailabilitiesAsync(id, upcomingOnly: true);
        return Ok(ApiResponse<List<TourAvailabilityResponse>>.Ok(result));
    }

    /// <summary>Tạo tour mới (Guide only).</summary>
    [HttpPost("tours")]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateTourRequest request)
    {
        var userId = GetCurrentUserId();
        var tour = await _tourService.CreateAsync(userId, request);
        return StatusCode(201, ApiResponse<TourResponse>.Ok(tour, "Tour created"));
    }

    /// <summary>Cập nhật tour (Guide, chủ sở hữu).</summary>
    [HttpPut("tours/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTourRequest request)
    {
        var userId = GetCurrentUserId();
        var tour = await _tourService.UpdateAsync(userId, id, request);
        return Ok(ApiResponse<TourResponse>.Ok(tour, "Tour updated"));
    }

    /// <summary>Xóa mềm tour (Guide, chủ sở hữu).</summary>
    [HttpDelete("tours/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        await _tourService.DeleteAsync(userId, id);
        return Ok(ApiResponse<object>.Ok(null!, "Tour deleted"));
    }

    /// <summary>Cập nhật trạng thái tour (Guide, chủ sở hữu).</summary>
    [HttpPut("tours/{id:guid}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTourStatusRequest request)
    {
        if (!Enum.TryParse<TourStatus>(request.Status, true, out var status))
            return BadRequest(ApiResponse<object>.Fail("Invalid status"));

        var userId = GetCurrentUserId();
        var tour = await _tourService.UpdateStatusAsync(userId, id, status);
        return Ok(ApiResponse<TourResponse>.Ok(tour, "Tour status updated"));
    }

    /// <summary>Danh sách tour của guide đang đăng nhập.</summary>
    [HttpGet("guides/me/tours")]
    [Authorize]
    public async Task<IActionResult> GetMyTours([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId = GetCurrentUserId();
        var (items, total) = await _tourService.GetGuideToursAsync(userId, page, size);
        var clampedSize = Math.Clamp(size, 1, 100);
        return Ok(ApiResponse<List<TourListItemResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(page, 1),
            Size = clampedSize,
            Total = total,
        }));
    }

    // --- Image management (Guide only) ---

    /// <summary>Upload 1-nhiều ảnh cho tour (max 10 tổng). Gửi files với key "files". Trả về danh sách URL hiện tại.</summary>
    [HttpPost("tours/{id:guid}/images")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImages(Guid id, [FromForm] List<IFormFile> files)
    {
        if (files is null || files.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("No files provided"));

        foreach (var f in files)
        {
            if (f.Length > MaxImageSize)
                return BadRequest(ApiResponse<object>.Fail($"File '{f.FileName}' exceeds 10 MB limit"));
            if (!AllowedImageTypes.Contains(f.ContentType))
                return BadRequest(ApiResponse<object>.Fail($"File '{f.FileName}' must be JPEG, PNG, or WebP"));
        }

        var userId = GetCurrentUserId();
        var uploadedUrls = new List<string>();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var path = $"{id}/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}{ext}";
            using var stream = file.OpenReadStream();
            var url = await _storage.UploadPublicAsync("tour-images", path, stream, file.ContentType);
            uploadedUrls.Add(url);
        }

        var images = await _tourService.AddImagesAsync(userId, id, [.. uploadedUrls]);
        return Ok(ApiResponse<string[]>.Ok(images, "Images uploaded"));
    }

    /// <summary>Xoá 1 ảnh khỏi tour.</summary>
    [HttpDelete("tours/{id:guid}/images")]
    [Authorize]
    public async Task<IActionResult> RemoveImage(Guid id, [FromBody] RemoveImageRequest request)
    {
        var userId = GetCurrentUserId();
        var images = await _tourService.RemoveImageAsync(userId, id, request.Url);
        return Ok(ApiResponse<string[]>.Ok(images, "Image removed"));
    }

    /// <summary>Upload ảnh bìa tour.</summary>
    [HttpPost("tours/{id:guid}/cover-image")]
    [Authorize]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadCoverImage(Guid id, [FromForm] SingleFileUploadRequest request)
    {
        var file = request.File;
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("File is required"));
        if (file.Length > MaxImageSize)
            return BadRequest(ApiResponse<object>.Fail("File exceeds 10 MB limit"));
        if (!AllowedImageTypes.Contains(file.ContentType))
            return BadRequest(ApiResponse<object>.Fail("Only JPEG, PNG, or WebP are allowed"));

        var userId = GetCurrentUserId();
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var path = $"{id}/cover{ext}";

        using var stream = file.OpenReadStream();
        var url = await _storage.UploadPublicAsync("tour-images", path, stream, file.ContentType);

        var coverUrl = await _tourService.UpdateCoverImageAsync(userId, id, url);
        return Ok(ApiResponse<string>.Ok(coverUrl, "Cover image updated"));
    }

    // --- Availability management (Guide only) ---

    /// <summary>Thêm ngày khả dụng cho tour (Guide, chủ sở hữu).</summary>
    [HttpPost("tours/{id:guid}/availabilities")]
    [Authorize]
    public async Task<IActionResult> CreateAvailability(Guid id, [FromBody] CreateAvailabilityRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _tourService.CreateAvailabilityAsync(userId, id, request);
        return StatusCode(201, ApiResponse<TourAvailabilityResponse>.Ok(result, "Availability created"));
    }

    /// <summary>Cập nhật ngày khả dụng (Guide, chủ sở hữu).</summary>
    [HttpPut("tours/{id:guid}/availabilities/{date}")]
    [Authorize]
    public async Task<IActionResult> UpdateAvailability(Guid id, DateOnly date, [FromBody] UpdateAvailabilityRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _tourService.UpdateAvailabilityAsync(userId, id, date, request);
        return Ok(ApiResponse<TourAvailabilityResponse>.Ok(result, "Availability updated"));
    }

    /// <summary>Xóa ngày khả dụng (Guide, chủ sở hữu).</summary>
    [HttpDelete("tours/{id:guid}/availabilities/{date}")]
    [Authorize]
    public async Task<IActionResult> DeleteAvailability(Guid id, DateOnly date)
    {
        var userId = GetCurrentUserId();
        await _tourService.DeleteAvailabilityAsync(userId, id, date);
        return Ok(ApiResponse<object>.Ok(null!, "Availability deleted"));
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
