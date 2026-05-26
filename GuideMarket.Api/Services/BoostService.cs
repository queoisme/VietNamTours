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
    private static readonly Dictionary<BoostPlan, (decimal Price, int Days, string Desc)> Plans = new()
    {
        [BoostPlan.basic]    = (50_000,  1, "SPONSORED badge, homepage priority"),
        [BoostPlan.standard] = (100_000, 3, "SPONSORED badge + color border"),
        [BoostPlan.premium]  = (200_000, 7, "Gold border, shadow, top placement"),
    };

    private readonly IUnitOfWork _uow;
    private readonly MomoClient  _momo;

    public BoostService(IUnitOfWork uow, MomoClient momo)
    {
        _uow  = uow;
        _momo = momo;
    }

    public List<BoostPlanInfo> GetPlans() =>
        Plans.Select(kv => new BoostPlanInfo(kv.Key.ToString(), kv.Value.Price, kv.Value.Days, kv.Value.Desc))
             .ToList();

    public async Task<MomoPaymentResponse> CreateAsync(Guid guideId, CreateBoostRequest request, string ipAddress)
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

        var (price, days, _) = Plans[plan];
        var txnRef = "bt" + Guid.NewGuid().ToString("N")[..13];
        var now    = DateTimeOffset.UtcNow;

        var boost = new Boost
        {
            Id           = Guid.NewGuid(),
            TourId       = request.TourId,
            GuideId      = guideId,
            Plan         = plan,
            PricePaid    = price,
            DurationDays = (short)days,
            StartsAt     = now,
            ExpiresAt    = now.AddDays(days),
            PaymentTxnId = txnRef,
            Status       = BoostStatus.cancelled,
        };

        await _uow.Boosts.AddAsync(boost);
        await _uow.SaveChangesAsync();

        var (payUrl, qrCodeUrl) = await _momo.CreatePaymentAsync(txnRef, price, $"Boost tour {tour.Title}");
        return new MomoPaymentResponse { PayUrl = payUrl, QrCodeUrl = qrCodeUrl };
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
        _uow.Boosts.Update(boost);

        var tour = await _uow.Tours.GetByIdAsync(boost.TourId);
        if (tour != null)
        {
            tour.IsBoosted      = true;
            tour.BoostExpiresAt = boost.ExpiresAt;
            _uow.Tours.Update(tour);
        }

        await _uow.SaveChangesAsync();
    }

    private static BoostResponse Map(Boost b) => new(
        b.Id, b.TourId, b.Tour?.Title ?? string.Empty,
        b.Plan.ToString(), b.PricePaid, b.DurationDays,
        b.StartsAt, b.ExpiresAt, b.Status.ToString());
}
