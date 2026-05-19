using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IWithdrawalService
{
    Task<FinanceResponse> GetFinanceAsync(Guid guideId);
    Task<(List<WithdrawalResponse> Items, long Total)> GetMyWithdrawalsAsync(Guid guideId, int page, int size);
    Task<WithdrawalResponse> CreateAsync(Guid guideId, CreateWithdrawalRequest request);
    Task<(List<AdminWithdrawalResponse> Items, long Total)> GetAllAsync(Guid adminId, string? status, int page, int size);
    Task<AdminWithdrawalResponse> ApproveAsync(Guid adminId, Guid withdrawalId, ProcessWithdrawalRequest request);
    Task<AdminWithdrawalResponse> RejectAsync(Guid adminId, Guid withdrawalId, ProcessWithdrawalRequest request);
}
