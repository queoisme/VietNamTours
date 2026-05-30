using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IHomeCategoryService
{
    Task<List<HomeCategoryResponse>> GetVisibleAsync();
    Task<List<HomeCategoryResponse>> GetAllAsync(Guid adminId);
    Task<HomeCategoryResponse> CreateAsync(Guid adminId, CreateHomeCategoryRequest request);
    Task<HomeCategoryResponse> UpdateAsync(Guid adminId, int id, UpdateHomeCategoryRequest request);
    Task DeleteAsync(Guid adminId, int id);
}
