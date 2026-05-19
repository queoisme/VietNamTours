using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IGuideProfileService
{
    Task<GuidePublicResponse?> GetPublicProfileAsync(Guid userId);
    Task<GuideProfileResponse?> GetOwnProfileAsync(Guid userId);
    Task<GuideProfileResponse> UpdateProfileAsync(Guid userId, UpdateGuideProfileRequest request);
}
