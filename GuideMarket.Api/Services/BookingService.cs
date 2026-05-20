using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class BookingService : IBookingService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<BookingService> _logger;
    private readonly INotificationService _notifications;

    public BookingService(IUnitOfWork uow, ILogger<BookingService> logger, INotificationService notifications)
    {
        _uow           = uow;
        _logger        = logger;
        _notifications = notifications;
    }

    public async Task<BookingDetailResponse> CreateAsync(Guid customerId, CreateBookingRequest request)
    {
        var user = await _uow.Users.GetByIdAsync(customerId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.customer)
            throw new ForbiddenAccessException("Only customers can create bookings");

        var tour = await _uow.Tours.GetByIdWithGuideAsync(request.TourId)
            ?? throw new KeyNotFoundException("Tour not found");
        if (tour.Status != TourStatus.active || tour.DeletedAt != null)
            throw new InvalidOperationException("Tour is not available for booking");
        if (tour.GuideId == customerId)
            throw new ForbiddenAccessException("You cannot book your own tour");

        var avail = await _uow.Tours.GetAvailabilityByDateAsync(request.TourId, request.TourDate)
            ?? throw new KeyNotFoundException("No availability found for the selected date");
        if (avail.IsBlocked)
            throw new InvalidOperationException("Selected date is not available");
        if ((avail.MaxSlots - avail.BookedSlots) < request.NumPeople)
            throw new InvalidOperationException("Not enough slots available");

        var totalPrice = tour.PricePerPerson * request.NumPeople;
        var txnRef     = Guid.NewGuid().ToString("N")[..15];

        var booking = new Booking
        {
            Id            = Guid.NewGuid(),
            TourId        = request.TourId,
            CustomerId    = customerId,
            GuideId       = tour.GuideId,
            TourDate      = request.TourDate,
            NumPeople     = request.NumPeople,
            TotalPrice    = totalPrice,
            ContactName   = request.ContactName,
            ContactPhone  = request.ContactPhone,
            ContactEmail  = request.ContactEmail,
            Note          = request.Note,
            Status        = BookingStatus.pending,
            PaymentStatus = PaymentStatus.unpaid,
            PaymentTxnId  = txnRef,
            CreatedAt     = DateTimeOffset.UtcNow,
            UpdatedAt     = DateTimeOffset.UtcNow,
        };

        await _uow.Bookings.AddAsync(booking);
        await _uow.SaveChangesAsync();

        var created = await _uow.Bookings.GetByIdWithDetailsAsync(booking.Id)
            ?? throw new InvalidOperationException("Failed to reload booking");

        await _notifications.CreateAsync(
            created.GuideId, "new_booking", "Bạn có booking mới!",
            $"Khách {created.Customer.FullName} đặt tour \"{created.Tour.Title}\" ngày {created.TourDate:dd/MM/yyyy}.",
            "booking", created.Id,
            "Booking mới - VietNamTours",
            $"<p>Khách hàng <strong>{created.Customer.FullName}</strong> đã đặt tour <strong>{created.Tour.Title}</strong> vào ngày {created.TourDate:dd/MM/yyyy}.</p>");

        return MapToDetail(created);
    }

    public async Task<(List<BookingListItemResponse> Items, long Total)> GetMyBookingsAsync(
        Guid customerId, string? status, int page, int size)
    {
        var (bookings, total) = await _uow.Bookings.GetByCustomerIdAsync(customerId, status, page, size);
        return (bookings.Select(MapToListItem).ToList(), total);
    }

    public async Task<BookingDetailResponse> GetByIdAsync(Guid requestingUserId, Guid bookingId)
    {
        var booking = await _uow.Bookings.GetByIdWithDetailsAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.CustomerId != requestingUserId && booking.GuideId != requestingUserId)
        {
            var user = await _uow.Users.GetByIdAsync(requestingUserId)
                ?? throw new KeyNotFoundException("User not found");
            if (user.Role != UserRole.admin)
                throw new ForbiddenAccessException();
        }

        return MapToDetail(booking);
    }

    public async Task<BookingDetailResponse> ConfirmAsync(Guid guideId, Guid bookingId)
    {
        var booking = await _uow.Bookings.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found");
        if (booking.GuideId != guideId)
            throw new ForbiddenAccessException();
        if (booking.Status != BookingStatus.pending)
            throw new InvalidOperationException("Booking cannot be confirmed in its current state");
        if (booking.PaymentStatus != PaymentStatus.paid)
            throw new InvalidOperationException("Booking must be paid before confirming");

        booking.Status      = BookingStatus.confirmed;
        booking.ConfirmedAt = DateTimeOffset.UtcNow;
        booking.UpdatedAt   = DateTimeOffset.UtcNow;

        var conversation = new Conversation
        {
            Id         = Guid.NewGuid(),
            BookingId  = booking.Id,
            CustomerId = booking.CustomerId,
            GuideId    = booking.GuideId,
            CreatedAt  = DateTimeOffset.UtcNow,
        };

        await _uow.Bookings.AddConversationAsync(conversation);
        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();

        var updated = await _uow.Bookings.GetByIdWithDetailsAsync(bookingId)
            ?? throw new InvalidOperationException("Failed to reload booking");

        await _notifications.CreateAsync(
            updated.CustomerId, "booking_confirmed", "Booking của bạn đã được xác nhận!",
            $"Tour \"{updated.Tour.Title}\" ngày {updated.TourDate:dd/MM/yyyy} đã được guide xác nhận.",
            "booking", updated.Id,
            "Booking xác nhận - VietNamTours",
            $"<p>Tour <strong>{updated.Tour.Title}</strong> vào ngày {updated.TourDate:dd/MM/yyyy} đã được guide xác nhận.</p>");

        return MapToDetail(updated);
    }

    public async Task<BookingDetailResponse> RejectAsync(Guid guideId, Guid bookingId, RejectBookingRequest request)
    {
        var booking = await _uow.Bookings.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found");
        if (booking.GuideId != guideId)
            throw new ForbiddenAccessException();
        if (booking.Status != BookingStatus.pending)
            throw new InvalidOperationException("Booking cannot be rejected in its current state");

        booking.Status           = BookingStatus.rejected;
        booking.RejectionReason  = request.Reason;
        booking.UpdatedAt        = DateTimeOffset.UtcNow;

        if (booking.PaymentStatus == PaymentStatus.paid)
        {
            booking.RefundAmount  = booking.TotalPrice;
            booking.RefundPolicy  = "100%";
            booking.PaymentStatus = PaymentStatus.refunded;

            await DecrementBookedSlotsAsync(booking.TourId, booking.TourDate, booking.NumPeople);
        }

        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();

        var updated = await _uow.Bookings.GetByIdWithDetailsAsync(bookingId)
            ?? throw new InvalidOperationException("Failed to reload booking");

        await _notifications.CreateAsync(
            updated.CustomerId, "booking_rejected", "Booking của bạn đã bị từ chối",
            $"Tour \"{updated.Tour.Title}\" ngày {updated.TourDate:dd/MM/yyyy} đã bị guide từ chối. Lý do: {request.Reason}",
            "booking", updated.Id,
            "Booking bị từ chối - VietNamTours",
            $"<p>Tour <strong>{updated.Tour.Title}</strong> ngày {updated.TourDate:dd/MM/yyyy} đã bị guide từ chối.</p><p>Lý do: {request.Reason}</p>");

        return MapToDetail(updated);
    }

    public async Task<BookingDetailResponse> CompleteAsync(Guid guideId, Guid bookingId)
    {
        var booking = await _uow.Bookings.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found");
        if (booking.GuideId != guideId)
            throw new ForbiddenAccessException();
        if (booking.Status != BookingStatus.confirmed)
            throw new InvalidOperationException("Booking cannot be completed in its current state");

        var guideProfile = await _uow.GuideProfiles.GetByUserIdAsync(guideId)
            ?? throw new KeyNotFoundException("Guide profile not found");

        var rate = guideProfile.SubscriptionPlan switch
        {
            SubscriptionPlan.pro     => 0.08m,
            SubscriptionPlan.premium => 0.10m,
            _                        => 0.15m,
        };
        var credit = booking.TotalPrice * (1 - rate);

        guideProfile.Balance      += credit;
        guideProfile.TotalEarned  += credit;
        booking.Status             = BookingStatus.completed;
        booking.CompletedAt        = DateTimeOffset.UtcNow;
        booking.UpdatedAt          = DateTimeOffset.UtcNow;

        _uow.GuideProfiles.Update(guideProfile);
        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();

        var updated = await _uow.Bookings.GetByIdWithDetailsAsync(bookingId)
            ?? throw new InvalidOperationException("Failed to reload booking");

        await _notifications.CreateAsync(
            updated.CustomerId, "booking_completed", "Tour đã hoàn thành!",
            $"Tour \"{updated.Tour.Title}\" ngày {updated.TourDate:dd/MM/yyyy} đã hoàn thành. Hãy để lại đánh giá của bạn!",
            "booking", updated.Id,
            "Tour hoàn thành - VietNamTours",
            $"<p>Tour <strong>{updated.Tour.Title}</strong> đã hoàn thành. Hãy để lại đánh giá của bạn!</p>");

        return MapToDetail(updated);
    }

    public async Task<BookingDetailResponse> CancelAsync(Guid requestingUserId, Guid bookingId, CancelBookingRequest request)
    {
        var booking = await _uow.Bookings.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        var user = await _uow.Users.GetByIdAsync(requestingUserId)
            ?? throw new KeyNotFoundException("User not found");

        var isCustomerOwner = booking.CustomerId == requestingUserId;
        var isAdmin         = user.Role == UserRole.admin;

        if (!isCustomerOwner && !isAdmin)
            throw new ForbiddenAccessException();

        if (booking.Status != BookingStatus.pending && booking.Status != BookingStatus.confirmed)
            throw new InvalidOperationException("Booking cannot be cancelled in its current state");

        decimal refundAmount = 0;
        string? refundPolicy = null;

        if (booking.PaymentStatus == PaymentStatus.paid)
        {
            if (booking.Status == BookingStatus.confirmed)
            {
                var tourDateTime  = booking.TourDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                var hoursUntilTour = (tourDateTime - DateTime.UtcNow).TotalHours;

                if (hoursUntilTour > 7 * 24)
                {
                    refundAmount = booking.TotalPrice;
                    refundPolicy = "100%";
                }
                else if (hoursUntilTour >= 48)
                {
                    refundAmount = booking.TotalPrice * 0.5m;
                    refundPolicy = "50%";
                }
                else
                {
                    refundAmount = 0;
                    refundPolicy = "0%";
                }
            }
            else
            {
                // pending + paid: guide never committed, full refund
                refundAmount = booking.TotalPrice;
                refundPolicy = "100%";
            }

            await DecrementBookedSlotsAsync(booking.TourId, booking.TourDate, booking.NumPeople);
        }

        booking.Status             = BookingStatus.cancelled;
        booking.CancellationBy     = isAdmin ? Models.CancellationBy.admin : Models.CancellationBy.customer;
        booking.CancellationReason = request.Reason;
        booking.RefundAmount       = refundAmount;
        booking.RefundPolicy       = refundPolicy;
        booking.UpdatedAt          = DateTimeOffset.UtcNow;

        if (refundAmount > 0)
            booking.PaymentStatus = PaymentStatus.refunded;

        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();

        var updated = await _uow.Bookings.GetByIdWithDetailsAsync(bookingId)
            ?? throw new InvalidOperationException("Failed to reload booking");

        // Notify the OTHER party about the cancellation
        if (isAdmin || isCustomerOwner)
        {
            // Notify guide if customer/admin cancelled
            await _notifications.CreateAsync(
                updated.GuideId, "booking_cancelled", "Booking đã bị huỷ",
                $"Booking tour \"{updated.Tour.Title}\" ngày {updated.TourDate:dd/MM/yyyy} đã bị huỷ.",
                "booking", updated.Id,
                "Booking bị huỷ - VietNamTours",
                $"<p>Booking tour <strong>{updated.Tour.Title}</strong> ngày {updated.TourDate:dd/MM/yyyy} đã bị huỷ.</p>");
        }

        return MapToDetail(updated);
    }

    public async Task<(List<BookingListItemResponse> Items, long Total)> GetGuideBookingsAsync(
        Guid guideId, string? status, int page, int size)
    {
        var user = await _uow.Users.GetByIdAsync(guideId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.guide)
            throw new ForbiddenAccessException("Only guides can access this endpoint");

        var (bookings, total) = await _uow.Bookings.GetByGuideIdAsync(guideId, status, page, size);
        return (bookings.Select(MapToListItem).ToList(), total);
    }

    public async Task HandlePaymentSuccessAsync(string txnId)
    {
        var booking = await _uow.Bookings.GetByPaymentTxnIdAsync(txnId);
        if (booking is null)
        {
            _logger.LogWarning("VNPay IPN: booking not found for txnId {TxnId}", txnId);
            return;
        }
        if (booking.PaymentStatus == PaymentStatus.paid)
            return; // already processed — idempotent

        booking.PaymentStatus = PaymentStatus.paid;
        booking.PaymentPaidAt = DateTimeOffset.UtcNow;
        booking.PaymentMethod = "vnpay";
        booking.UpdatedAt     = DateTimeOffset.UtcNow;

        var avail = await _uow.Tours.GetAvailabilityByDateAsync(booking.TourId, booking.TourDate);
        if (avail is not null)
        {
            if (avail.BookedSlots + booking.NumPeople > avail.MaxSlots)
            {
                _logger.LogWarning(
                    "VNPay IPN: slot overflow for booking {BookingId}, tour {TourId}",
                    booking.Id, booking.TourId);
            }
            else
            {
                avail.BookedSlots += booking.NumPeople;
                _uow.Tours.UpdateAvailability(avail);
            }
        }

        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();
    }

    private async Task DecrementBookedSlotsAsync(Guid tourId, DateOnly tourDate, short numPeople)
    {
        var avail = await _uow.Tours.GetAvailabilityByDateAsync(tourId, tourDate);
        if (avail is not null)
        {
            avail.BookedSlots = (short)Math.Max(0, avail.BookedSlots - numPeople);
            _uow.Tours.UpdateAvailability(avail);
        }
    }

    private static BookingListItemResponse MapToListItem(Booking b) => new()
    {
        Id                = b.Id,
        TourId            = b.TourId,
        TourTitle         = b.Tour.Title,
        TourCoverImageUrl = b.Tour.CoverImageUrl,
        TourDate          = b.TourDate,
        NumPeople         = b.NumPeople,
        TotalPrice        = b.TotalPrice,
        Status            = b.Status.ToString(),
        PaymentStatus     = b.PaymentStatus.ToString(),
        CreatedAt         = b.CreatedAt,
    };

    private static BookingDetailResponse MapToDetail(Booking b) => new()
    {
        Id                = b.Id,
        TourId            = b.TourId,
        TourTitle         = b.Tour.Title,
        TourCoverImageUrl = b.Tour.CoverImageUrl,
        CustomerId        = b.CustomerId,
        CustomerName      = b.Customer.FullName,
        GuideId           = b.GuideId,
        GuideName         = b.Guide.FullName,
        TourDate          = b.TourDate,
        NumPeople         = b.NumPeople,
        TotalPrice        = b.TotalPrice,
        ContactName       = b.ContactName,
        ContactPhone      = b.ContactPhone,
        ContactEmail      = b.ContactEmail,
        Note              = b.Note,
        Status            = b.Status.ToString(),
        RejectionReason   = b.RejectionReason,
        CancellationBy    = b.CancellationBy?.ToString(),
        CancellationReason = b.CancellationReason,
        RefundAmount      = b.RefundAmount,
        RefundPolicy      = b.RefundPolicy,
        PaymentStatus     = b.PaymentStatus.ToString(),
        PaymentMethod     = b.PaymentMethod,
        PaymentTxnId      = b.PaymentTxnId,
        PaymentPaidAt     = b.PaymentPaidAt,
        ConfirmedAt       = b.ConfirmedAt,
        CompletedAt       = b.CompletedAt,
        CreatedAt         = b.CreatedAt,
        UpdatedAt         = b.UpdatedAt,
    };
}
