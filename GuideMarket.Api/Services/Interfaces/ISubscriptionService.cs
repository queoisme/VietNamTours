using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface ISubscriptionService
{
    List<SubscriptionPlanInfo> GetPlans();
    Task<string> CreateAsync(Guid guideId, CreateSubscriptionRequest request, string ipAddress);
    Task<SubscriptionResponse?> GetMySubscriptionAsync(Guid guideId);
    Task HandlePaymentSuccessAsync(string txnRef);
}
