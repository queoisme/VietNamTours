using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Infrastructure;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class BoostService : IBoostService
{
    private readonly IUnitOfWork _uow;
    private readonly VnPayClient _vnpay;

    public BoostService(IUnitOfWork uow, VnPayClient vnpay)
    {
        _uow   = uow;
        _vnpay = vnpay;
    }

    public async Task<List<BoostPlanInfo>> GetPlansAsync()
    {
        var configs = await _uow.BoostPlanConfigs.GetAllActiveAsync();
        return configs.Select(c => new BoostPlanInfo(c.Plan, c.Price, c.Days, c.Description)).ToList();
    }

    public async Task<VnPayPaymentResponse> CreateAsync(Guid guideId, CreateBoostRequest request, string ipAddress)
    {
        var user = await _uow.Users.GetByIdAsync(guideId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.guide)
            throw new ForbiddenAccessException("Only guides can boost tours");

        var tour = await _uow.Tours.GetByIdAsync(request.TourId)
            ?? throw new KeyNotFoundException("Tour not found");
        if (tour.GuideId != guideId)
            throw new ForbiddenAccessException("Not your tour");
        if (tour.DeletedAt != null)
            throw new KeyNotFoundException("Tour not found");

        if (!Enum.TryParse<BoostPlan>(request.Plan, true, out var plan))
            throw new InvalidOperationException("Invalid boost plan");

        var config = await _uow.BoostPlanConfigs.GetByPlanAsync(request.Plan.ToLower())
            ?? throw new InvalidOperationException("Boost plan config not found");
        var (price, days) = (config.Price, config.Days);
        var txnRef = "bt" + Guid.NewGuid().ToString("N")[..13];
        var now    = DateTimeOffset.UtcNow;

        var boost = new Boost
        {
            Id           = Guid.NewGuid(),
            TourId       = request.TourId,
            GuideId      = guideId,
            Plan         = plan,
            PricePaid    = price,
            DurationDays = (short)(int)days,
            StartsAt     = now,
            ExpiresAt    = now.AddDays(days),
            PaymentTxnId = txnRef,
            Status       = BoostStatus.cancelled,
        };

        await _uow.Boosts.AddAsync(boost);
        await _uow.SaveChangesAsync();

        var payUrl = _vnpay.CreatePaymentUrl(txnRef, price, $"Boost tour {tour.Title}", ipAddress);
        return new VnPayPaymentResponse { PayUrl = payUrl };
    }

    public async Task<(List<BoostResponse> Items, long Total)> GetMyBoostsAsync(Guid guideId, int page, int size)
    {
        var (items, total) = await _uow.Boosts.GetByGuideIdAsync(guideId, page, size);
        return (items.Select(Map).ToList(), total);
    }

    public async Task HandlePaymentSuccessAsync(string txnRef, string paymentMethod = "momo")
    {
        var boost = await _uow.Boosts.GetByPaymentTxnIdAsync(txnRef);
        if (boost == null || boost.Status == BoostStatus.active) return;

        boost.Status = BoostStatus.active;

        var tour = await _uow.Tours.GetByIdAsync(boost.TourId);
        if (tour != null)
        {
            var now = DateTimeOffset.UtcNow;
            var baseDate = (tour.IsBoosted && tour.BoostExpiresAt.HasValue && tour.BoostExpiresAt > now)
                ? tour.BoostExpiresAt.Value
                : now;
            var newExpiry = baseDate.AddDays(boost.DurationDays);

            tour.IsBoosted      = true;
            tour.BoostExpiresAt = newExpiry;
            boost.StartsAt      = now;
            boost.ExpiresAt     = newExpiry;

            _uow.Tours.Update(tour);
        }

        _uow.Boosts.Update(boost);

        await _uow.SaveChangesAsync();
    }

    public async Task<BoostPlanInfo> UpdatePlanAsync(Guid adminId, string plan, UpdateBoostPlanRequest request)
    {
        var admin = await _uow.Users.GetByIdAsync(adminId)
            ?? throw new KeyNotFoundException("User not found");
        if (admin.Role != UserRole.admin)
            throw new ForbiddenAccessException("Only admins can update boost plans");

        var config = await _uow.BoostPlanConfigs.GetByPlanAsync(plan.ToLower())
            ?? throw new KeyNotFoundException($"Boost plan '{plan}' không tồn tại");

        if (request.Price.HasValue)          config.Price       = request.Price.Value;
        if (request.Days.HasValue)           config.Days        = request.Days.Value;
        if (request.Description is not null) config.Description = request.Description;
        if (request.IsActive.HasValue)       config.IsActive    = request.IsActive.Value;
        config.UpdatedAt = DateTimeOffset.UtcNow;

        _uow.BoostPlanConfigs.Update(config);
        await _uow.SaveChangesAsync();

        return new BoostPlanInfo(config.Plan, config.Price, config.Days, config.Description);
    }

    private static BoostResponse Map(Boost b) => new(
        b.Id, b.TourId, b.Tour?.Title ?? string.Empty,
        b.Plan.ToString(), b.PricePaid, b.DurationDays,
        b.StartsAt, b.ExpiresAt, b.Status.ToString());
}
