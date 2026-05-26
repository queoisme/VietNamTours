using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface ISubscriptionService
{
    Task<List<SubscriptionPlanInfo>> GetPlansAsync();
    Task<MomoPaymentResponse> CreateAsync(Guid guideId, CreateSubscriptionRequest request, string ipAddress);
    Task<SubscriptionResponse?> GetMySubscriptionAsync(Guid guideId);
    Task HandlePaymentSuccessAsync(string txnRef, string paymentMethod = "momo");
    Task<SubscriptionPlanInfo> UpdatePlanAsync(Guid adminId, string plan, UpdateSubscriptionPlanRequest request);
}
