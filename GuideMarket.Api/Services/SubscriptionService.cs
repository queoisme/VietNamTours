using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Infrastructure;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class SubscriptionService : ISubscriptionService
{
    private static readonly Dictionary<SubscriptionPlan, (decimal Price, int Days, string Desc)> Plans = new()
    {
        [SubscriptionPlan.premium] = (299_000,  30,  "10% commission, unlimited tours"),
        [SubscriptionPlan.pro]     = (799_000,  90,  "8% commission, unlimited tours, priority support"),
    };

    private readonly IUnitOfWork _uow;
    private readonly VnPayClient _vnPay;

    public SubscriptionService(IUnitOfWork uow, VnPayClient vnPay)
    {
        _uow   = uow;
        _vnPay = vnPay;
    }

    public List<SubscriptionPlanInfo> GetPlans() =>
        Plans.Select(kv => new SubscriptionPlanInfo(kv.Key.ToString(), kv.Value.Price, kv.Value.Days, kv.Value.Desc))
             .ToList();

    public async Task<string> CreateAsync(Guid guideId, CreateSubscriptionRequest request, string ipAddress)
    {
        var user = await _uow.Users.GetByIdAsync(guideId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.guide)
            throw new ForbiddenAccessException("Only guides can subscribe");

        if (!Enum.TryParse<SubscriptionPlan>(request.Plan, true, out var plan) || plan == SubscriptionPlan.free)
            throw new InvalidOperationException("Invalid subscription plan");

        var (price, days, _) = Plans[plan];
        var txnRef = "sb" + Guid.NewGuid().ToString("N")[..13];
        var now    = DateTimeOffset.UtcNow;

        var sub = new Subscription
        {
            Id           = Guid.NewGuid(),
            GuideId      = guideId,
            Plan         = plan,
            PricePaid    = price,
            StartsAt     = now,
            ExpiresAt    = now.AddDays(days),
            PaymentTxnId = txnRef,
            Status       = BoostStatus.cancelled,
        };

        await _uow.Subscriptions.AddAsync(sub);
        await _uow.SaveChangesAsync();

        return _vnPay.CreatePaymentUrl(txnRef, price, $"Subscription {plan}", ipAddress);
    }

    public async Task<SubscriptionResponse?> GetMySubscriptionAsync(Guid guideId)
    {
        var sub = await _uow.Subscriptions.GetActiveByGuideIdAsync(guideId);
        return sub == null ? null : MapSub(sub);
    }

    public async Task HandlePaymentSuccessAsync(string txnRef)
    {
        var sub = await _uow.Subscriptions.GetByPaymentTxnIdAsync(txnRef);
        if (sub == null || sub.Status == BoostStatus.active) return;

        sub.Status = BoostStatus.active;
        _uow.Subscriptions.Update(sub);

        var profile = await _uow.GuideProfiles.GetByUserIdAsync(sub.GuideId);
        if (profile != null)
        {
            profile.SubscriptionPlan      = sub.Plan;
            profile.SubscriptionExpiresAt = sub.ExpiresAt;
            _uow.GuideProfiles.Update(profile);
        }

        await _uow.SaveChangesAsync();
    }

    private static SubscriptionResponse MapSub(Subscription s) => new(
        s.Id, s.Plan.ToString(), s.PricePaid, s.StartsAt, s.ExpiresAt, s.Status.ToString());
}
