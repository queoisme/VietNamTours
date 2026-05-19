using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class TourService : ITourService
{
    private readonly IUnitOfWork _uow;

    public TourService(IUnitOfWork uow) => _uow = uow;

    public async Task<(List<TourListItemResponse> Items, long Total)> SearchAsync(TourSearchParams p)
    {
        var (tours, total) = await _uow.Tours.SearchAsync(p, publicOnly: true);
        return (tours.Select(MapToListItem).ToList(), total);
    }

    public async Task<TourResponse?> GetByIdAsync(Guid id, Guid? requestingUserId = null)
    {
        var tour = await _uow.Tours.GetByIdWithGuideAsync(id);
        if (tour is null) return null;

        // Public: chỉ trả về tour active
        // Guide owner: có thể xem draft/inactive của mình
        var isOwner = requestingUserId.HasValue && tour.GuideId == requestingUserId.Value;
        if (tour.Status != TourStatus.active && !isOwner) return null;

        return MapToResponse(tour);
    }

    public async Task<TourResponse> CreateAsync(Guid userId, CreateTourRequest request)
    {
        await RequireGuideAsync(userId);

        if (!Enum.TryParse<TourCategory>(request.Category, true, out var category))
            throw new InvalidOperationException($"Invalid category: {request.Category}");

        var slug = await GenerateUniqueSlugAsync(request.Title);

        var tour = new Tour
        {
            Id = Guid.NewGuid(),
            GuideId = userId,
            Title = request.Title,
            Slug = slug,
            Description = request.Description,
            Category = category,
            LocationCity = request.LocationCity,
            LocationAddress = request.LocationAddress,
            Lat = request.Lat,
            Lng = request.Lng,
            PricePerPerson = request.PricePerPerson,
            DurationHours = request.DurationHours,
            MaxGroupSize = request.MaxGroupSize,
            Highlights = request.Highlights,
            Included = request.Included,
            Excluded = request.Excluded,
            Itinerary = JsonSerializer.Serialize(request.Itinerary),
            Images = request.Images,
            CoverImageUrl = request.CoverImageUrl,
            Status = TourStatus.draft,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        await _uow.Tours.AddAsync(tour);
        await _uow.SaveChangesAsync();

        // Reload with guide navigation for response
        var created = await _uow.Tours.GetByIdWithGuideAsync(tour.Id)
            ?? throw new KeyNotFoundException("Tour not found after creation");
        return MapToResponse(created);
    }

    public async Task<TourResponse> UpdateAsync(Guid userId, Guid tourId, UpdateTourRequest request)
    {
        var tour = await GetOwnedTourAsync(userId, tourId);

        if (request.Title is not null) tour.Title = request.Title;
        if (request.Description is not null) tour.Description = request.Description;
        if (request.Category is not null)
        {
            if (!Enum.TryParse<TourCategory>(request.Category, true, out var cat))
                throw new InvalidOperationException($"Invalid category: {request.Category}");
            tour.Category = cat;
        }
        if (request.LocationCity is not null) tour.LocationCity = request.LocationCity;
        if (request.LocationAddress is not null) tour.LocationAddress = request.LocationAddress;
        if (request.Lat.HasValue) tour.Lat = request.Lat;
        if (request.Lng.HasValue) tour.Lng = request.Lng;
        if (request.PricePerPerson.HasValue) tour.PricePerPerson = request.PricePerPerson.Value;
        if (request.DurationHours.HasValue) tour.DurationHours = request.DurationHours.Value;
        if (request.MaxGroupSize.HasValue) tour.MaxGroupSize = request.MaxGroupSize.Value;
        if (request.Highlights is not null) tour.Highlights = request.Highlights;
        if (request.Included is not null) tour.Included = request.Included;
        if (request.Excluded is not null) tour.Excluded = request.Excluded;
        if (request.Itinerary is not null) tour.Itinerary = JsonSerializer.Serialize(request.Itinerary);
        if (request.Images is not null) tour.Images = request.Images;
        if (request.CoverImageUrl is not null) tour.CoverImageUrl = request.CoverImageUrl;

        _uow.Tours.Update(tour);
        await _uow.SaveChangesAsync();

        var updated = await _uow.Tours.GetByIdWithGuideAsync(tourId)
            ?? throw new KeyNotFoundException("Tour not found");
        return MapToResponse(updated);
    }

    public async Task DeleteAsync(Guid userId, Guid tourId)
    {
        var tour = await GetOwnedTourAsync(userId, tourId);
        tour.DeletedAt = DateTimeOffset.UtcNow;
        _uow.Tours.Update(tour);
        await _uow.SaveChangesAsync();
    }

    public async Task<TourResponse> UpdateStatusAsync(Guid userId, Guid tourId, TourStatus status)
    {
        var tour = await GetOwnedTourAsync(userId, tourId);

        if (status == TourStatus.active && tour.Status != TourStatus.active)
        {
            var profile = await _uow.GuideProfiles.GetByUserIdAsync(userId);
            if (profile?.SubscriptionPlan == SubscriptionPlan.free)
            {
                var activeCount = await _uow.Tours.CountActiveByGuideAsync(userId);
                if (activeCount >= 5)
                    throw new InvalidOperationException(
                        "Free plan allows a maximum of 5 active tours. Upgrade to premium or pro to add more.");
            }
        }

        tour.Status = status;
        _uow.Tours.Update(tour);
        await _uow.SaveChangesAsync();

        var updated = await _uow.Tours.GetByIdWithGuideAsync(tourId)
            ?? throw new KeyNotFoundException("Tour not found");
        return MapToResponse(updated);
    }

    public async Task<(List<TourListItemResponse> Items, long Total)> GetGuideToursAsync(Guid userId, int page, int size)
    {
        await RequireGuideAsync(userId);
        var (tours, total) = await _uow.Tours.GetByGuideIdAsync(userId, page, size);
        return (tours.Select(MapToListItem).ToList(), total);
    }

    // --- Availabilities ---

    public async Task<List<TourAvailabilityResponse>> GetAvailabilitiesAsync(Guid tourId, bool upcomingOnly)
    {
        var tour = await _uow.Tours.GetByIdAsync(tourId)
            ?? throw new KeyNotFoundException("Tour not found");
        var avails = await _uow.Tours.GetAvailabilitiesAsync(tourId, upcomingOnly);
        return avails.Select(MapAvailability).ToList();
    }

    public async Task<TourAvailabilityResponse> CreateAvailabilityAsync(Guid userId, Guid tourId, CreateAvailabilityRequest request)
    {
        await GetOwnedTourAsync(userId, tourId);

        var existing = await _uow.Tours.GetAvailabilityByDateAsync(tourId, request.AvailableDate);
        if (existing is not null)
            throw new InvalidOperationException($"Availability for {request.AvailableDate} already exists");

        var avail = new TourAvailability
        {
            Id = Guid.NewGuid(),
            TourId = tourId,
            AvailableDate = request.AvailableDate,
            MaxSlots = request.IsBlocked ? (short)0 : request.MaxSlots,
            BookedSlots = 0,
            IsBlocked = request.IsBlocked,
        };

        await _uow.Tours.AddAvailabilityAsync(avail);
        await _uow.SaveChangesAsync();
        return MapAvailability(avail);
    }

    public async Task<TourAvailabilityResponse> UpdateAvailabilityAsync(Guid userId, Guid tourId, DateOnly date, UpdateAvailabilityRequest request)
    {
        await GetOwnedTourAsync(userId, tourId);

        var avail = await _uow.Tours.GetAvailabilityByDateAsync(tourId, date)
            ?? throw new KeyNotFoundException($"Availability for {date} not found");

        if (request.MaxSlots.HasValue)
        {
            if (request.MaxSlots.Value < avail.BookedSlots)
                throw new InvalidOperationException("Cannot set max slots below already booked slots");
            avail.MaxSlots = request.MaxSlots.Value;
        }
        if (request.IsBlocked.HasValue) avail.IsBlocked = request.IsBlocked.Value;

        _uow.Tours.UpdateAvailability(avail);
        await _uow.SaveChangesAsync();
        return MapAvailability(avail);
    }

    public async Task DeleteAvailabilityAsync(Guid userId, Guid tourId, DateOnly date)
    {
        await GetOwnedTourAsync(userId, tourId);

        var avail = await _uow.Tours.GetAvailabilityByDateAsync(tourId, date)
            ?? throw new KeyNotFoundException($"Availability for {date} not found");

        if (avail.BookedSlots > 0)
            throw new InvalidOperationException("Cannot delete availability with existing bookings");

        _uow.Tours.DeleteAvailability(avail);
        await _uow.SaveChangesAsync();
    }

    // --- Helpers ---

    private async Task RequireGuideAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.guide)
            throw new ForbiddenAccessException("Only guides can perform this action");
    }

    private async Task<Tour> GetOwnedTourAsync(Guid userId, Guid tourId)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.guide)
            throw new ForbiddenAccessException("Only guides can perform this action");

        var tour = await _uow.Tours.GetByIdAsync(tourId)
            ?? throw new KeyNotFoundException("Tour not found");
        if (tour.GuideId != userId)
            throw new ForbiddenAccessException("You do not own this tour");

        return tour;
    }

    private async Task<string> GenerateUniqueSlugAsync(string title)
    {
        var baseSlug = Regex.Replace(title.ToLowerInvariant(), @"[^a-z0-9\s-]", "").Trim();
        baseSlug = Regex.Replace(baseSlug.Replace(' ', '-'), @"-+", "-").TrimEnd('-');

        string slug;
        int attempts = 0;
        do
        {
            var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToLower();
            slug = $"{baseSlug}-{suffix}";
            attempts++;
            if (attempts > 10) throw new InvalidOperationException("Failed to generate unique slug");
        }
        while (await _uow.Tours.SlugExistsAsync(slug));

        return slug;
    }

    private static TourListItemResponse MapToListItem(Tour t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Slug = t.Slug,
        CoverImageUrl = t.CoverImageUrl,
        LocationCity = t.LocationCity,
        Category = t.Category.ToString(),
        PricePerPerson = t.PricePerPerson,
        DurationHours = t.DurationHours,
        MaxGroupSize = t.MaxGroupSize,
        AvgRating = t.AvgRating,
        TotalReviews = t.TotalReviews,
        TotalBookings = t.TotalBookings,
        IsBoosted = t.IsBoosted,
        Status = t.Status.ToString(),
        Guide = new TourGuideInfo
        {
            Id = t.Guide.Id,
            FullName = t.Guide.FullName,
            AvatarUrl = t.Guide.AvatarUrl,
        },
        CreatedAt = t.CreatedAt,
    };

    private static TourResponse MapToResponse(Tour t) => new()
    {
        Id = t.Id,
        Title = t.Title,
        Slug = t.Slug,
        Description = t.Description,
        Category = t.Category.ToString(),
        LocationCity = t.LocationCity,
        LocationAddress = t.LocationAddress,
        Lat = t.Lat,
        Lng = t.Lng,
        PricePerPerson = t.PricePerPerson,
        DurationHours = t.DurationHours,
        MaxGroupSize = t.MaxGroupSize,
        Highlights = t.Highlights,
        Included = t.Included,
        Excluded = t.Excluded,
        Itinerary = JsonSerializer.Deserialize<List<ItineraryItem>>(t.Itinerary) ?? [],
        Images = t.Images,
        CoverImageUrl = t.CoverImageUrl,
        Status = t.Status.ToString(),
        AvgRating = t.AvgRating,
        TotalReviews = t.TotalReviews,
        TotalBookings = t.TotalBookings,
        IsBoosted = t.IsBoosted,
        BoostExpiresAt = t.BoostExpiresAt,
        Guide = new TourGuideInfo
        {
            Id = t.Guide.Id,
            FullName = t.Guide.FullName,
            AvatarUrl = t.Guide.AvatarUrl,
        },
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
    };

    private static TourAvailabilityResponse MapAvailability(TourAvailability a) => new()
    {
        Id = a.Id,
        TourId = a.TourId,
        AvailableDate = a.AvailableDate,
        MaxSlots = a.MaxSlots,
        BookedSlots = a.BookedSlots,
        IsBlocked = a.IsBlocked,
    };
}
