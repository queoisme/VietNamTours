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
    private readonly IUnitOfWork _uow;
    private readonly MomoClient  _momo;

    public SubscriptionService(IUnitOfWork uow, MomoClient momo)
    {
        _uow  = uow;
        _momo = momo;
    }

    // ----------------------------------------------------------------
    // Public: lấy danh sách plans từ DB
    // ----------------------------------------------------------------
    public async Task<List<SubscriptionPlanInfo>> GetPlansAsync()
    {
        var configs = await _uow.SubscriptionPlanConfigs.GetAllActiveAsync();
        return configs
            .Select(c => new SubscriptionPlanInfo(c.Plan, c.Price, c.Days, c.Description))
            .ToList();
    }

    // ----------------------------------------------------------------
    // Admin: cập nhật giá / số ngày / mô tả một plan
    // ----------------------------------------------------------------
    public async Task<SubscriptionPlanInfo> UpdatePlanAsync(Guid adminId, string plan, UpdateSubscriptionPlanRequest request)
    {
        var admin = await _uow.Users.GetByIdAsync(adminId)
            ?? throw new KeyNotFoundException("User not found");
        if (admin.Role != UserRole.admin)
            throw new ForbiddenAccessException("Only admins can update subscription plans");

        var config = await _uow.SubscriptionPlanConfigs.GetByPlanAsync(plan.ToLower())
            ?? throw new KeyNotFoundException($"Plan '{plan}' không tồn tại");

        if (request.Price.HasValue)       config.Price       = request.Price.Value;
        if (request.Days.HasValue)        config.Days        = request.Days.Value;
        if (request.Description is not null) config.Description = request.Description;
        if (request.IsActive.HasValue)    config.IsActive    = request.IsActive.Value;
        config.UpdatedAt = DateTimeOffset.UtcNow;

        _uow.SubscriptionPlanConfigs.Update(config);
        await _uow.SaveChangesAsync();

        return new SubscriptionPlanInfo(config.Plan, config.Price, config.Days, config.Description);
    }

    // ----------------------------------------------------------------
    // Guide: mua subscription
    // ----------------------------------------------------------------
    public async Task<MomoPaymentResponse> CreateAsync(Guid guideId, CreateSubscriptionRequest request, string ipAddress)
    {
        var user = await _uow.Users.GetByIdAsync(guideId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.guide)
            throw new ForbiddenAccessException("Only guides can subscribe");

        var config = await _uow.SubscriptionPlanConfigs.GetByPlanAsync(request.Plan.ToLower())
            ?? throw new InvalidOperationException("Invalid subscription plan");

        if (!Enum.TryParse<SubscriptionPlan>(config.Plan, true, out var plan) || plan == SubscriptionPlan.free)
            throw new InvalidOperationException("Invalid subscription plan");

        var txnRef = "sb" + Guid.NewGuid().ToString("N")[..13];
        var now    = DateTimeOffset.UtcNow;

        var sub = new Subscription
        {
            Id           = Guid.NewGuid(),
            GuideId      = guideId,
            Plan         = plan,
            PricePaid    = config.Price,
            StartsAt     = now,
            ExpiresAt    = now.AddDays(config.Days),
            PaymentTxnId = txnRef,
            Status       = BoostStatus.cancelled,
        };

        await _uow.Subscriptions.AddAsync(sub);
        await _uow.SaveChangesAsync();

        var (payUrl, qrCodeUrl) = await _momo.CreatePaymentAsync(txnRef, config.Price, $"Subscription {plan}");
        return new MomoPaymentResponse { PayUrl = payUrl, QrCodeUrl = qrCodeUrl };
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
