using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IGuideApplicationService
{
    Task<GuideApplicationResponse> SubmitAsync(Guid userId, CreateGuideApplicationRequest request);
    Task<GuideApplicationResponse?> GetMyLatestApplicationAsync(Guid userId);
    Task<(List<GuideApplicationResponse> Items, long Total)> GetAllAsync(Guid adminId, GuideApplicationListParams p);
    Task<GuideApplicationResponse?> GetByIdAsync(Guid adminId, Guid applicationId);
    Task<GuideApplicationResponse> ApproveAsync(Guid adminId, Guid applicationId);
    Task<GuideApplicationResponse> RejectAsync(Guid adminId, Guid applicationId, RejectApplicationRequest request);
}
