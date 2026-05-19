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

    public WithdrawalService(IUnitOfWork uow) => _uow = uow;

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
