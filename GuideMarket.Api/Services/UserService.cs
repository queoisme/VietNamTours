using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _uow;

    public UserService(IUnitOfWork uow) => _uow = uow;

    public async Task<UserResponse?> GetByIdAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);
        return user is null ? null : MapToResponse(user);
    }

    public async Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");

        _uow.Users.Update(user);
        if (request.FullName is not null) user.FullName = request.FullName;
        if (request.Phone is not null) user.Phone = request.Phone;

        await _uow.SaveChangesAsync();
        return MapToResponse(user);
    }

    public async Task<UserResponse> UpdateAvatarAsync(Guid userId, string avatarUrl)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");

        user.AvatarUrl = avatarUrl;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();
        return MapToResponse(user);
    }

    private static UserResponse MapToResponse(Models.User u) => new()
    {
        Id = u.Id,
        Email = u.Email,
        FullName = u.FullName,
        Phone = u.Phone,
        AvatarUrl = u.AvatarUrl,
        Role = u.Role.ToString(),
        IsVerified = u.IsVerified,
        IsBanned = u.IsBanned,
        CreatedAt = u.CreatedAt,
    };
}
