using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class WithdrawalService : IWithdrawalService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;

    public WithdrawalService(IUnitOfWork uow, INotificationService notifications)
    {
        _uow           = uow;
        _notifications = notifications;
    }

    public async Task<FinanceResponse> GetFinanceAsync(Guid guideId)
    {
        var profile = await _uow.GuideProfiles.GetByUserIdAsync(guideId)
            ?? throw new KeyNotFoundException("Guide profile not found");

        return new FinanceResponse(
            profile.Balance,
            profile.TotalEarned,
            profile.TotalWithdrawn,
            profile.SubscriptionPlan.ToString(),
            profile.SubscriptionExpiresAt);
    }

    public async Task<(List<WithdrawalResponse> Items, long Total)> GetMyWithdrawalsAsync(
        Guid guideId, int page, int size)
    {
        var (items, total) = await _uow.Withdrawals.GetByGuideIdAsync(guideId, page, size);
        return (items.Select(Map).ToList(), total);
    }

    public async Task<WithdrawalResponse> CreateAsync(Guid guideId, CreateWithdrawalRequest request)
    {
        var user = await _uow.Users.GetByIdAsync(guideId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.guide)
            throw new ForbiddenAccessException("Only guides can withdraw");

        var profile = await _uow.GuideProfiles.GetByUserIdAsync(guideId)
            ?? throw new KeyNotFoundException("Guide profile not found");

        if (request.Amount > profile.Balance)
            throw new InvalidOperationException("Insufficient balance");

        if (!Enum.TryParse<WithdrawalMethod>(request.Method, true, out var method))
            throw new InvalidOperationException("Invalid withdrawal method");

        var fee       = Math.Round(request.Amount * 0.02m, 0);
        var netAmount = request.Amount - fee;

        // Deduct immediately to prevent double withdrawal
        profile.Balance -= request.Amount;
        _uow.GuideProfiles.Update(profile);

        var withdrawal = new Withdrawal
        {
            Id          = Guid.NewGuid(),
            GuideId     = guideId,
            Amount      = request.Amount,
            Fee         = fee,
            NetAmount   = netAmount,
            Method      = method,
            AccountInfo = request.AccountInfo,
            Note        = request.Note,
            Status      = WithdrawalStatus.pending,
            CreatedAt   = DateTimeOffset.UtcNow,
        };

        await _uow.Withdrawals.AddAsync(withdrawal);
        await _uow.SaveChangesAsync();

        return Map(withdrawal);
    }

    public async Task<(List<AdminWithdrawalResponse> Items, long Total)> GetAllAsync(
        Guid adminId, string? status, int page, int size)
    {
        await RequireAdminAsync(adminId);
        var (items, total) = await _uow.Withdrawals.GetAllAsync(status, page, size);
        return (items.Select(MapAdmin).ToList(), total);
    }

    public async Task<AdminWithdrawalResponse> ApproveAsync(Guid adminId, Guid withdrawalId, ProcessWithdrawalRequest request)
    {
        await RequireAdminAsync(adminId);

        var w = await _uow.Withdrawals.GetByIdWithGuideAsync(withdrawalId)
            ?? throw new KeyNotFoundException("Withdrawal not found");

        if (w.Status != WithdrawalStatus.pending)
            throw new InvalidOperationException("Withdrawal is not pending");

        w.Status      = WithdrawalStatus.approved;
        w.AdminNote   = request.AdminNote;
        w.ProcessedAt = DateTimeOffset.UtcNow;

        var profile = await _uow.GuideProfiles.GetByUserIdAsync(w.GuideId);
        if (profile != null)
        {
            profile.TotalWithdrawn += w.Amount;
            _uow.GuideProfiles.Update(profile);
        }

        _uow.Withdrawals.Update(w);
        await _uow.SaveChangesAsync();

        await _notifications.CreateAsync(
            w.GuideId, "withdrawal_approved", "Yêu cầu rút tiền được chấp thuận",
            $"Yêu cầu rút {w.Amount:N0} VNĐ đã được duyệt. Số tiền thực nhận: {w.NetAmount:N0} VNĐ.",
            "withdrawal", w.Id,
            "Rút tiền được chấp thuận - GuideMarket",
            $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:24px;border:1px solid #e0e0e0;border-radius:8px">
              <h2 style="color:#2e7d32">Yêu cầu rút tiền được duyệt ✓</h2>
              <p>Yêu cầu rút tiền của bạn đã được admin xử lý.</p>
              <table style="width:100%;border-collapse:collapse;margin:16px 0">
                <tr><td style="padding:8px;color:#666">Số tiền yêu cầu</td><td style="padding:8px;text-align:right"><strong>{w.Amount:N0} VNĐ</strong></td></tr>
                <tr><td style="padding:8px;color:#666">Phí giao dịch (2%)</td><td style="padding:8px;text-align:right">{w.Fee:N0} VNĐ</td></tr>
                <tr style="background:#f5f5f5"><td style="padding:8px"><strong>Thực nhận</strong></td><td style="padding:8px;text-align:right"><strong style="color:#2e7d32">{w.NetAmount:N0} VNĐ</strong></td></tr>
              </table>
              {(string.IsNullOrWhiteSpace(w.AdminNote) ? "" : $"<p style='color:#666'>Ghi chú: {w.AdminNote}</p>")}
            </div>
            """);

        return MapAdmin(w);
    }

    public async Task<AdminWithdrawalResponse> RejectAsync(Guid adminId, Guid withdrawalId, ProcessWithdrawalRequest request)
    {
        await RequireAdminAsync(adminId);

        var w = await _uow.Withdrawals.GetByIdWithGuideAsync(withdrawalId)
            ?? throw new KeyNotFoundException("Withdrawal not found");

        if (w.Status != WithdrawalStatus.pending)
            throw new InvalidOperationException("Withdrawal is not pending");

        w.Status      = WithdrawalStatus.rejected;
        w.AdminNote   = request.AdminNote;
        w.ProcessedAt = DateTimeOffset.UtcNow;

        // Refund balance on rejection
        var profile = await _uow.GuideProfiles.GetByUserIdAsync(w.GuideId);
        if (profile != null)
        {
            profile.Balance += w.Amount;
            _uow.GuideProfiles.Update(profile);
        }

        _uow.Withdrawals.Update(w);
        await _uow.SaveChangesAsync();

        await _notifications.CreateAsync(
            w.GuideId, "withdrawal_rejected", "Yêu cầu rút tiền bị từ chối",
            $"Yêu cầu rút {w.Amount:N0} VNĐ đã bị từ chối. Số tiền đã được hoàn lại vào số dư của bạn.",
            "withdrawal", w.Id,
            "Rút tiền bị từ chối - GuideMarket",
            $"""
            <div style="font-family:Arial,sans-serif;max-width:480px;margin:auto;padding:24px;border:1px solid #e0e0e0;border-radius:8px">
              <h2 style="color:#c62828">Yêu cầu rút tiền bị từ chối</h2>
              <p>Yêu cầu rút <strong>{w.Amount:N0} VNĐ</strong> của bạn đã bị từ chối.</p>
              <p>Số tiền đã được <strong>hoàn lại vào số dư</strong> tài khoản của bạn.</p>
              {(string.IsNullOrWhiteSpace(w.AdminNote) ? "" : $"<p><strong>Lý do:</strong> {w.AdminNote}</p>")}
              <p style="color:#666;font-size:14px">Vui lòng liên hệ hỗ trợ nếu bạn có thắc mắc.</p>
            </div>
            """);

        return MapAdmin(w);
    }

    private async Task RequireAdminAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.admin)
            throw new ForbiddenAccessException("Admin only");
    }

    private static WithdrawalResponse Map(Withdrawal w) => new(
        w.Id, w.Amount, w.Fee, w.NetAmount,
        w.Method.ToString(), w.AccountInfo, w.Note,
        w.Status.ToString(), w.AdminNote, w.CreatedAt, w.ProcessedAt);

    private static AdminWithdrawalResponse MapAdmin(Withdrawal w) => new(
        w.Id, w.GuideId,
        w.Guide?.FullName ?? string.Empty,
        w.Guide?.Email ?? string.Empty,
        w.Amount, w.Fee, w.NetAmount,
        w.Method.ToString(), w.AccountInfo, w.Note,
        w.Status.ToString(), w.AdminNote, w.CreatedAt, w.ProcessedAt);
}
