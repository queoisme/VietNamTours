using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

/// <summary>
/// Upload files cho guide application (identity doc, certificates).
/// Dùng private bucket — chỉ admin và owner mới xem được qua signed URL.
/// </summary>
[ApiController]
[Route("api/v1/uploads")]
[Authorize]
public class UploadsController : ControllerBase
{
    private readonly SupabaseStorageClient _storage;

    private static readonly string[] AllowedDocTypes =
        ["image/jpeg", "image/png", "image/webp", "application/pdf"];
    private const long MaxDocSize = 10 * 1024 * 1024; // 10 MB

    public UploadsController(SupabaseStorageClient storage) => _storage = storage;

    /// <summary>Upload CCCD / hộ chiếu (1 file, private). Trả về storage path để dùng trong guide-applications.</summary>
    [HttpPost("identity-doc")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadIdentityDoc([FromForm] IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("File is required"));
        if (file.Length > MaxDocSize)
            return BadRequest(ApiResponse<object>.Fail("File exceeds 10 MB limit"));
        if (!AllowedDocTypes.Contains(file.ContentType))
            return BadRequest(ApiResponse<object>.Fail("Only JPEG, PNG, WebP, or PDF are allowed"));

        var userId = GetCurrentUserId();
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var path = $"{userId}/identity/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{ext}";

        using var stream = file.OpenReadStream();
        var storagePath = await _storage.UploadPrivateAsync("guide-documents", path, stream, file.ContentType);

        return Ok(ApiResponse<UploadResponse>.Ok(new UploadResponse(storagePath), "Identity document uploaded"));
    }

    /// <summary>Upload chứng chỉ hướng dẫn (nhiều file, private). Trả về mảng storage path.</summary>
    [HttpPost("certificates")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadCertificates()
    {
        var files = Request.Form.Files;
        if (files.Count == 0)
            return BadRequest(ApiResponse<object>.Fail("No files provided"));
        if (files.Count > 5)
            return BadRequest(ApiResponse<object>.Fail("Maximum 5 certificate files allowed"));

        foreach (var f in files)
        {
            if (f.Length > MaxDocSize)
                return BadRequest(ApiResponse<object>.Fail($"File '{f.FileName}' exceeds 10 MB limit"));
            if (!AllowedDocTypes.Contains(f.ContentType))
                return BadRequest(ApiResponse<object>.Fail($"File '{f.FileName}' must be JPEG, PNG, WebP, or PDF"));
        }

        var userId = GetCurrentUserId();
        var paths = new List<string>();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var path = $"{userId}/certificates/{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Guid.NewGuid():N}{ext}";
            using var stream = file.OpenReadStream();
            var storagePath = await _storage.UploadPrivateAsync("guide-documents", path, stream, file.ContentType);
            paths.Add(storagePath);
        }

        return Ok(ApiResponse<string[]>.Ok([.. paths], "Certificates uploaded"));
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
