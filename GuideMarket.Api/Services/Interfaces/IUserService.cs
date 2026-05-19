using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IUserService
{
    Task<UserResponse?> GetByIdAsync(Guid userId);
    Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request);
    Task<UserResponse> UpdateAvatarAsync(Guid userId, string avatarUrl);
}
