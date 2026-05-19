using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class ReviewService : IReviewService
{
    private readonly IUnitOfWork _uow;

    public ReviewService(IUnitOfWork uow) => _uow = uow;

    public async Task<ReviewResponse> CreateAsync(Guid customerId, CreateReviewRequest request)
    {
        var booking = await _uow.Bookings.GetByIdWithDetailsAsync(request.BookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.CustomerId != customerId)
            throw new ForbiddenAccessException("Not your booking");

        if (booking.Status != BookingStatus.completed)
            throw new InvalidOperationException("Can only review completed bookings");

        var existing = await _uow.Reviews.GetByBookingIdAsync(request.BookingId);
        if (existing != null)
            throw new InvalidOperationException("You have already reviewed this booking");

        var review = new Review
        {
            Id         = Guid.NewGuid(),
            BookingId  = request.BookingId,
            TourId     = booking.TourId,
            CustomerId = customerId,
            GuideId    = booking.GuideId,
            Rating     = request.Rating,
            Comment    = request.Comment,
            IsVisible  = true,
            CreatedAt  = DateTimeOffset.UtcNow,
        };

        await _uow.Reviews.AddAsync(review);
        await _uow.SaveChangesAsync();

        return MapToResponse(review, booking.Customer, booking.Tour);
    }

    public async Task<ReviewResponse> ReplyAsync(Guid guideId, Guid reviewId, ReplyReviewRequest request)
    {
        var review = await _uow.Reviews.GetByIdWithDetailsAsync(reviewId)
            ?? throw new KeyNotFoundException("Review not found");

        if (review.GuideId != guideId)
            throw new ForbiddenAccessException("Not your tour review");

        if (review.GuideReply != null)
            throw new InvalidOperationException("Reply already exists");

        var tracked = await _uow.Reviews.GetByIdAsync(reviewId)
            ?? throw new KeyNotFoundException("Review not found");

        tracked.GuideReply = request.Reply;
        _uow.Reviews.Update(tracked);
        await _uow.SaveChangesAsync();

        return MapToResponse(tracked, review.Customer, review.Tour);
    }

    public async Task<(List<ReviewResponse> Items, long Total)> GetByTourIdAsync(Guid tourId, int page, int size)
    {
        var (items, total) = await _uow.Reviews.GetByTourIdAsync(tourId, page, size);
        return (items.Select(r => MapToResponse(r, r.Customer, r.Tour)).ToList(), total);
    }

    public async Task<(List<ReviewResponse> Items, long Total)> GetMyReviewsAsync(Guid customerId, int page, int size)
    {
        var (items, total) = await _uow.Reviews.GetByCustomerIdAsync(customerId, page, size);
        return (items.Select(r => MapToResponse(r, r.Customer, r.Tour)).ToList(), total);
    }

    public async Task<ReviewResponse> ToggleVisibilityAsync(Guid adminId, Guid reviewId)
    {
        var user = await _uow.Users.GetByIdAsync(adminId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.admin)
            throw new ForbiddenAccessException("Admin only");

        var review = await _uow.Reviews.GetByIdAsync(reviewId)
            ?? throw new KeyNotFoundException("Review not found");

        review.IsVisible = !review.IsVisible;
        _uow.Reviews.Update(review);
        await _uow.SaveChangesAsync();

        var detail = await _uow.Reviews.GetByIdWithDetailsAsync(reviewId)
            ?? throw new KeyNotFoundException("Review not found");
        return MapToResponse(review, detail.Customer, detail.Tour);
    }

    private static ReviewResponse MapToResponse(Review r, User customer, Tour tour) => new(
        r.Id, r.BookingId, r.TourId, tour.Title,
        r.CustomerId, customer.FullName, customer.AvatarUrl,
        r.Rating, r.Comment, r.GuideReply, r.IsVisible, r.CreatedAt);
}
