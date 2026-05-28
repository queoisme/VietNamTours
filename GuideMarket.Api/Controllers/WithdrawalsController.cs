using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class WithdrawalsController : ControllerBase
{
    private readonly IWithdrawalService _withdrawals;

    public WithdrawalsController(IWithdrawalService withdrawals) => _withdrawals = withdrawals;

    [HttpGet("guides/me/finance")]
    public async Task<IActionResult> GetFinance()
    {
        var userId  = GetCurrentUserId();
        var finance = await _withdrawals.GetFinanceAsync(userId);
        return Ok(ApiResponse<FinanceResponse>.Ok(finance));
    }

    [HttpGet("withdrawals/my")]
    public async Task<IActionResult> GetMine([FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId         = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _withdrawals.GetMyWithdrawalsAsync(userId, page, clampedSize);
        return Ok(ApiResponse<List<WithdrawalResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(page, 1), Size = clampedSize, Total = total,
        }));
    }

    [HttpPost("withdrawals")]
    public async Task<IActionResult> Create([FromBody] CreateWithdrawalRequest request)
    {
        var userId     = GetCurrentUserId();
        var withdrawal = await _withdrawals.CreateAsync(userId, request);
        return StatusCode(201, ApiResponse<WithdrawalResponse>.Ok(withdrawal, "Withdrawal request submitted"));
    }

    [HttpGet("admin/withdrawals")]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int size = 20)
    {
        var userId         = GetCurrentUserId();
        var clampedSize    = Math.Clamp(size, 1, 100);
        var (items, total) = await _withdrawals.GetAllAsync(userId, status, page, clampedSize);
        return Ok(ApiResponse<List<AdminWithdrawalResponse>>.Ok(items, meta: new PaginationMeta
        {
            Page = Math.Max(page, 1), Size = clampedSize, Total = total,
        }));
    }

    [HttpPost("admin/withdrawals/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ProcessWithdrawalRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _withdrawals.ApproveAsync(userId, id, request);
        return Ok(ApiResponse<AdminWithdrawalResponse>.Ok(result, "Withdrawal approved"));
    }

    [HttpPost("admin/withdrawals/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ProcessWithdrawalRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _withdrawals.RejectAsync(userId, id, request);
        return Ok(ApiResponse<AdminWithdrawalResponse>.Ok(result, "Withdrawal rejected"));
    }

    [HttpPost("admin/withdrawals/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] ProcessWithdrawalRequest request)
    {
        var userId = GetCurrentUserId();
        var result = await _withdrawals.CompleteAsync(userId, id, request);
        return Ok(ApiResponse<AdminWithdrawalResponse>.Ok(result, "Withdrawal completed"));
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
