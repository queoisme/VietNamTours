using System.Text.Json;
using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class GuideProfileService : IGuideProfileService
{
    private readonly IUnitOfWork _uow;

    public GuideProfileService(IUnitOfWork uow) => _uow = uow;

    public async Task<GuidePublicResponse?> GetPublicProfileAsync(Guid userId)
    {
        var profile = await _uow.GuideProfiles.GetByUserIdWithUserAsync(userId);
        if (profile is null || profile.VerificationStatus != VerificationStatus.approved) return null;
        return MapToPublic(profile);
    }

    public async Task<GuideProfileResponse?> GetOwnProfileAsync(Guid userId)
    {
        var profile = await _uow.GuideProfiles.GetByUserIdWithUserAsync(userId);
        return profile is null ? null : MapToFull(profile);
    }

    public async Task<GuideProfileResponse> UpdateProfileAsync(Guid userId, UpdateGuideProfileRequest request)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.guide)
            throw new ForbiddenAccessException("Only guides can update guide profiles");

        var profile = await _uow.GuideProfiles.GetByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Guide profile not found");

        if (request.Bio is not null) profile.Bio = request.Bio;
        if (request.ExperienceYears.HasValue) profile.ExperienceYears = request.ExperienceYears.Value;
        if (request.Languages is not null) profile.Languages = request.Languages;
        if (request.Certifications is not null)
            profile.Certifications = JsonSerializer.Serialize(request.Certifications);

        _uow.GuideProfiles.Update(profile);
        await _uow.SaveChangesAsync();

        var updated = await _uow.GuideProfiles.GetByUserIdWithUserAsync(userId)!
            ?? throw new KeyNotFoundException("Guide profile not found");
        return MapToFull(updated);
    }

    private static GuidePublicResponse MapToPublic(GuideProfile p) => new()
    {
        UserId = p.UserId,
        FullName = p.User.FullName,
        AvatarUrl = p.User.AvatarUrl,
        Bio = p.Bio,
        ExperienceYears = p.ExperienceYears,
        Languages = p.Languages,
        Certifications = DeserializeCerts(p.Certifications),
        AvgRating = p.AvgRating,
        TotalReviews = p.TotalReviews,
        SubscriptionPlan = p.SubscriptionPlan.ToString(),
    };

    private static GuideProfileResponse MapToFull(GuideProfile p) => new()
    {
        ProfileId = p.Id,
        UserId = p.UserId,
        FullName = p.User.FullName,
        AvatarUrl = p.User.AvatarUrl,
        Bio = p.Bio,
        ExperienceYears = p.ExperienceYears,
        Languages = p.Languages,
        Certifications = DeserializeCerts(p.Certifications),
        AvgRating = p.AvgRating,
        TotalReviews = p.TotalReviews,
        SubscriptionPlan = p.SubscriptionPlan.ToString(),
        VerificationStatus = p.VerificationStatus.ToString(),
        RejectionReason = p.RejectionReason,
        Balance = p.Balance,
        TotalEarned = p.TotalEarned,
        TotalWithdrawn = p.TotalWithdrawn,
        SubscriptionExpiresAt = p.SubscriptionExpiresAt,
    };

    private static List<CertificationItem> DeserializeCerts(string json)
    {
        try { return JsonSerializer.Deserialize<List<CertificationItem>>(json) ?? []; }
        catch { return []; }
    }
}
