using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IBoostService
{
    Task<List<BoostPlanInfo>> GetPlansAsync();
    Task<VnPayPaymentResponse> CreateAsync(Guid guideId, CreateBoostRequest request, string ipAddress);
    Task<(List<BoostResponse> Items, long Total)> GetMyBoostsAsync(Guid guideId, int page, int size);
    Task HandlePaymentSuccessAsync(string txnRef, string paymentMethod = "momo");
    Task<BoostPlanInfo> UpdatePlanAsync(Guid adminId, string plan, UpdateBoostPlanRequest request);
}
