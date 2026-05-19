using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.Services.Interfaces;

public interface IBookingService
{
    Task<BookingDetailResponse> CreateAsync(Guid customerId, CreateBookingRequest request);
    Task<(List<BookingListItemResponse> Items, long Total)> GetMyBookingsAsync(
        Guid customerId, string? status, int page, int size);
    Task<BookingDetailResponse> GetByIdAsync(Guid requestingUserId, Guid bookingId);
    Task<BookingDetailResponse> ConfirmAsync(Guid guideId, Guid bookingId);
    Task<BookingDetailResponse> RejectAsync(Guid guideId, Guid bookingId, RejectBookingRequest request);
    Task<BookingDetailResponse> CompleteAsync(Guid guideId, Guid bookingId);
    Task<BookingDetailResponse> CancelAsync(Guid requestingUserId, Guid bookingId, CancelBookingRequest request);
    Task<(List<BookingListItemResponse> Items, long Total)> GetGuideBookingsAsync(
        Guid guideId, string? status, int page, int size);
    Task HandlePaymentSuccessAsync(string txnId);
}
