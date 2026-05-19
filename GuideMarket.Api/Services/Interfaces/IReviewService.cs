using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IReviewService
{
    Task<ReviewResponse> CreateAsync(Guid customerId, CreateReviewRequest request);
    Task<ReviewResponse> ReplyAsync(Guid guideId, Guid reviewId, ReplyReviewRequest request);
    Task<(List<ReviewResponse> Items, long Total)> GetByTourIdAsync(Guid tourId, int page, int size);
    Task<(List<ReviewResponse> Items, long Total)> GetMyReviewsAsync(Guid customerId, int page, int size);
    Task<ReviewResponse> ToggleVisibilityAsync(Guid adminId, Guid reviewId);
}
