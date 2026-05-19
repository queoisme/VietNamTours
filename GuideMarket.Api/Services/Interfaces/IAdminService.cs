using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IAdminService
{
    Task<AdminStatsResponse> GetStatsAsync(Guid adminId);
    Task<AdminRevenueResponse> GetRevenueAsync(Guid adminId, DateOnly? from, DateOnly? to);
    Task<(List<AdminUserResponse> Items, long Total)> GetUsersAsync(Guid adminId, string? role, string? q, int page, int size);
    Task<AdminUserResponse> GetUserByIdAsync(Guid adminId, Guid userId);
    Task BanUserAsync(Guid adminId, Guid userId, BanUserRequest request);
    Task<(List<AdminTourResponse> Items, long Total)> GetToursAsync(Guid adminId, string? status, int page, int size);
    Task UpdateTourStatusAsync(Guid adminId, Guid tourId, UpdateTourStatusRequest request);
    Task<byte[]> ExportBookingsAsync(Guid adminId, DateOnly? from, DateOnly? to, string? status);
}
