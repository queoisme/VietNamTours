using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace GuideMarket.Api.Services;

public class HomeCategoryService : IHomeCategoryService
{
    private readonly IUnitOfWork _uow;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "home_category_displays";

    public HomeCategoryService(IUnitOfWork uow, IMemoryCache cache)
    {
        _uow   = uow;
        _cache = cache;
    }

    public async Task<List<HomeCategoryResponse>> GetVisibleAsync()
    {
        return await _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var items = await _uow.HomeCategoryDisplays.GetAllVisibleAsync();
            return items.Select(Map).ToList();
        }) ?? [];
    }

    public async Task<List<HomeCategoryResponse>> GetAllAsync(Guid adminId)
    {
        await RequireAdminAsync(adminId);
        var items = await _uow.HomeCategoryDisplays.GetAllAsync();
        return items.Select(Map).ToList();
    }

    public async Task<HomeCategoryResponse> CreateAsync(Guid adminId, CreateHomeCategoryRequest request)
    {
        await RequireAdminAsync(adminId);

        if (!Enum.TryParse<TourCategory>(request.CategoryFilter, ignoreCase: true, out var filter))
            throw new InvalidOperationException($"Giá trị danh mục '{request.CategoryFilter}' không hợp lệ");

        var entity = new HomeCategoryDisplay
        {
            Name           = request.Name.Trim(),
            Description    = request.Description.Trim(),
            CategoryFilter = filter,
            IsVisible      = request.IsVisible,
            SortOrder      = request.SortOrder,
            CreatedAt      = DateTimeOffset.UtcNow,
            UpdatedAt      = DateTimeOffset.UtcNow,
        };

        await _uow.HomeCategoryDisplays.AddAsync(entity);
        await _uow.SaveChangesAsync();

        _cache.Remove(CacheKey);
        return Map(entity);
    }

    public async Task<HomeCategoryResponse> UpdateAsync(Guid adminId, int id, UpdateHomeCategoryRequest request)
    {
        await RequireAdminAsync(adminId);

        var entity = await _uow.HomeCategoryDisplays.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy danh mục với id={id}");

        if (request.Name is not null)        entity.Name        = request.Name.Trim();
        if (request.Description is not null) entity.Description = request.Description.Trim();
        if (request.IsVisible.HasValue)      entity.IsVisible   = request.IsVisible.Value;
        if (request.SortOrder.HasValue)      entity.SortOrder   = request.SortOrder.Value;

        if (request.CategoryFilter is not null)
        {
            if (!Enum.TryParse<TourCategory>(request.CategoryFilter, ignoreCase: true, out var filter))
                throw new InvalidOperationException($"Giá trị danh mục '{request.CategoryFilter}' không hợp lệ");
            entity.CategoryFilter = filter;
        }

        entity.UpdatedAt = DateTimeOffset.UtcNow;
        _uow.HomeCategoryDisplays.Update(entity);
        await _uow.SaveChangesAsync();

        _cache.Remove(CacheKey);
        return Map(entity);
    }

    public async Task DeleteAsync(Guid adminId, int id)
    {
        await RequireAdminAsync(adminId);

        var entity = await _uow.HomeCategoryDisplays.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Không tìm thấy danh mục với id={id}");

        _uow.HomeCategoryDisplays.Remove(entity);
        await _uow.SaveChangesAsync();

        _cache.Remove(CacheKey);
    }

    private async Task RequireAdminAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.admin)
            throw new ForbiddenAccessException("Only admins can perform this action");
    }

    private static HomeCategoryResponse Map(HomeCategoryDisplay c) => new()
    {
        Id             = c.Id,
        Name           = c.Name,
        Description    = c.Description,
        CategoryFilter = c.CategoryFilter.ToString(),
        IsVisible      = c.IsVisible,
        SortOrder      = c.SortOrder,
        CreatedAt      = c.CreatedAt,
        UpdatedAt      = c.UpdatedAt,
    };
}
