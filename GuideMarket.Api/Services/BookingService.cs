using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Infrastructure;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class BookingService : IBookingService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<BookingService> _logger;
    private readonly INotificationService _notifications;
    private readonly VnPayClient _vnpay;

    public BookingService(IUnitOfWork uow, ILogger<BookingService> logger, INotificationService notifications, VnPayClient vnpay)
    {
        _uow           = uow;
        _logger        = logger;
        _notifications = notifications;
        _vnpay         = vnpay;
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

        if (request.NumPeople > tour.MaxGroupSize)
            throw new InvalidOperationException($"Number of people exceeds this tour's maximum group size of {tour.MaxGroupSize}");

        var isPrivate = tour.TourType == TourType.@private;

        // Atomically reserve slots — eliminates race condition between concurrent booking requests
        var slotIncrement = isPrivate ? (short)1 : (short)request.NumPeople;
        var reserved = await _uow.Tours.TryIncrementBookedSlotsAsync(
            request.TourId, request.TourDate, slotIncrement);
        if (!reserved)
            throw new InvalidOperationException(
                isPrivate
                    ? "This private tour date is already booked"
                    : "Not enough slots available for the selected date");

        var numDays    = isPrivate ? (short)Math.Max(1, (int)Math.Ceiling((double)tour.DurationHours / 24.0)) : (short)1;
        var totalPrice = tour.PricePerPerson * request.NumPeople * numDays;
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

        // Auto-block consecutive dates for multi-day private tours (single batch query)
        if (isPrivate && numDays > 1)
        {
            var endDate = request.TourDate.AddDays(numDays - 1);
            var subsequentAvails = await _uow.Tours.GetAvailabilitiesByDateRangeAsync(
                request.TourId, request.TourDate.AddDays(1), endDate);

            foreach (var nextAvail in subsequentAvails.Where(a => !a.IsBlocked))
            {
                nextAvail.IsBlocked = true;
                nextAvail.BlockedByBookingId = booking.Id;
                _uow.Tours.UpdateAvailability(nextAvail);
            }
        }

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
        var reviewedIds = await _uow.Reviews.GetReviewedBookingIdsAsync(bookings.Select(b => b.Id));
        return (bookings.Select(b => MapToListItem(b, reviewedIds.Contains(b.Id))).ToList(), total);
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

        // Upsert conversation: link existing one or create new
        var existing = await _uow.Conversations.FirstOrDefaultAsync(
            c => c.CustomerId == booking.CustomerId && c.GuideId == booking.GuideId);
        if (existing != null)
        {
            existing.BookingId = booking.Id;
            _uow.Conversations.Update(existing);
        }
        else
        {
            await _uow.Conversations.AddAsync(new Conversation
            {
                Id         = Guid.NewGuid(),
                BookingId  = booking.Id,
                CustomerId = booking.CustomerId,
                GuideId    = booking.GuideId,
                CreatedAt  = DateTimeOffset.UtcNow,
            });
        }

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
        booking.CancellationBy   = Models.CancellationBy.guide;
        booking.RejectionReason  = request.Reason;
        booking.UpdatedAt        = DateTimeOffset.UtcNow;

        var rejectTour = await _uow.Tours.GetByIdAsync(booking.TourId);
        var rejectIsPrivate = rejectTour?.TourType == TourType.@private;

        // Always release the slot — reserved atomically at booking creation regardless of payment status
        await DecrementBookedSlotsAsync(booking.TourId, booking.TourDate, booking.NumPeople, rejectIsPrivate);

        if (booking.PaymentStatus == PaymentStatus.paid)
        {
            booking.RefundAmount  = booking.TotalPrice;
            booking.RefundPolicy  = "100%";
            booking.PaymentStatus = PaymentStatus.refunded;
        }

        if (rejectIsPrivate)
            await UnblockConsecutiveDatesAsync(booking.Id);

        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();

        if (booking.RefundAmount > 0)
            await TryExecuteVnpayRefundAsync(booking);

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

        var cancelTour = await _uow.Tours.GetByIdAsync(booking.TourId);
        var cancelIsPrivate = cancelTour?.TourType == TourType.@private;

        // Always release the slot — reserved atomically at booking creation regardless of payment status
        await DecrementBookedSlotsAsync(booking.TourId, booking.TourDate, booking.NumPeople, cancelIsPrivate);

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
        }

        if (cancelIsPrivate)
            await UnblockConsecutiveDatesAsync(booking.Id);

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

        if (refundAmount > 0)
            await TryExecuteVnpayRefundAsync(booking);

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
        return (bookings.Select(b => MapToListItem(b)).ToList(), total);
    }

    public async Task HandlePaymentSuccessAsync(string txnId, string paymentMethod = "momo", string? vnpayTransactionNo = null)
    {
        var booking = await _uow.Bookings.GetByPaymentTxnIdAsync(txnId);
        if (booking is null)
        {
            _logger.LogWarning("Payment IPN: booking not found for txnId {TxnId}", txnId);
            return;
        }
        if (booking.PaymentStatus == PaymentStatus.paid)
            return; // already processed — idempotent

        if (booking.Status == BookingStatus.cancelled)
        {
            _logger.LogWarning(
                "Payment IPN: booking {BookingId} already cancelled — initiating refund for late payment {TxnId}",
                booking.Id, txnId);

            // Save payment info so TryExecuteVnpayRefundAsync can issue the refund
            booking.PaymentMethod = paymentMethod;
            booking.PaymentPaidAt = DateTimeOffset.UtcNow;
            booking.PaymentStatus = PaymentStatus.refunded;
            booking.RefundAmount  = booking.TotalPrice;
            booking.RefundPolicy  = "100%";
            booking.UpdatedAt     = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(vnpayTransactionNo))
                booking.VnpayTransactionNo = vnpayTransactionNo;

            _uow.Bookings.Update(booking);
            await _uow.SaveChangesAsync();

            await TryExecuteVnpayRefundAsync(booking);

            await _notifications.CreateAsync(
                booking.CustomerId,
                "booking_cancelled",
                "Đặt tour đã huỷ — đang hoàn tiền",
                $"Đơn đặt tour đã bị huỷ nhưng hệ thống đã nhận được thanh toán. Chúng tôi đang hoàn lại {booking.TotalPrice:N0} VNĐ cho bạn.",
                "booking",
                booking.Id);

            return;
        }

        booking.PaymentStatus = PaymentStatus.paid;
        booking.PaymentPaidAt = DateTimeOffset.UtcNow;
        booking.PaymentMethod = paymentMethod;
        booking.UpdatedAt     = DateTimeOffset.UtcNow;

        if (!string.IsNullOrWhiteSpace(vnpayTransactionNo))
            booking.VnpayTransactionNo = vnpayTransactionNo;

        // Slots are reserved atomically at booking creation for all tour types — no increment needed here.
        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();
    }

    private async Task DecrementBookedSlotsAsync(Guid tourId, DateOnly tourDate, short numPeople, bool isPrivate = false)
    {
        var avail = await _uow.Tours.GetAvailabilityByDateAsync(tourId, tourDate);
        if (avail is not null)
        {
            // Private tours track "1 booking slot", not numPeople
            var decrement = isPrivate ? (short)1 : numPeople;
            avail.BookedSlots = (short)Math.Max(0, avail.BookedSlots - decrement);
            _uow.Tours.UpdateAvailability(avail);
        }
    }

    private async Task UnblockConsecutiveDatesAsync(Guid bookingId)
    {
        var blocked = await _uow.Tours.GetAvailabilitiesBlockedByBookingAsync(bookingId);
        foreach (var avail in blocked)
        {
            avail.IsBlocked = false;
            avail.BlockedByBookingId = null;
            _uow.Tours.UpdateAvailability(avail);
        }
    }

    public async Task<BookingDetailResponse> GuideCancelAsync(Guid guideId, Guid bookingId, CancelBookingRequest request)
    {
        var booking = await _uow.Bookings.GetByIdAsync(bookingId)
            ?? throw new KeyNotFoundException("Booking not found");

        if (booking.GuideId != guideId)
            throw new ForbiddenAccessException();

        if (booking.Status != BookingStatus.confirmed)
            throw new InvalidOperationException("Booking cannot be cancelled in its current state");

        decimal refundAmount = 0;
        string? refundPolicy = null;

        var guideCancelTour = await _uow.Tours.GetByIdAsync(booking.TourId);
        var guideCancelIsPrivate = guideCancelTour?.TourType == TourType.@private;

        // Always release the slot — reserved atomically at booking creation regardless of payment status
        await DecrementBookedSlotsAsync(booking.TourId, booking.TourDate, booking.NumPeople, guideCancelIsPrivate);

        if (booking.PaymentStatus == PaymentStatus.paid)
        {
            refundAmount = booking.TotalPrice;
            refundPolicy = "100%";
        }

        if (guideCancelIsPrivate)
            await UnblockConsecutiveDatesAsync(booking.Id);

        booking.Status             = BookingStatus.cancelled;
        booking.CancellationBy     = Models.CancellationBy.guide;
        booking.CancellationReason = request.Reason;
        booking.RefundAmount       = refundAmount;
        booking.RefundPolicy       = refundPolicy;
        booking.UpdatedAt          = DateTimeOffset.UtcNow;

        if (refundAmount > 0)
            booking.PaymentStatus = PaymentStatus.refunded;

        _uow.Bookings.Update(booking);
        await _uow.SaveChangesAsync();

        if (refundAmount > 0)
            await TryExecuteVnpayRefundAsync(booking);

        var updated = await _uow.Bookings.GetByIdWithDetailsAsync(bookingId)
            ?? throw new InvalidOperationException("Failed to reload booking");

        var refundNote = refundAmount > 0 ? " Bạn sẽ được hoàn lại 100% số tiền đã thanh toán." : "";
        await _notifications.CreateAsync(
            updated.CustomerId, "booking_cancelled", "Booking của bạn đã bị guide huỷ",
            $"Tour \"{updated.Tour.Title}\" ngày {updated.TourDate:dd/MM/yyyy} đã bị guide huỷ. Lý do: {request.Reason}.{refundNote}",
            "booking", updated.Id,
            "Booking bị guide huỷ - VietNamTours",
            $"<p>Tour <strong>{updated.Tour.Title}</strong> ngày {updated.TourDate:dd/MM/yyyy} đã bị guide huỷ.</p><p>Lý do: {request.Reason}</p>{(refundAmount > 0 ? "<p>Bạn sẽ được hoàn lại <strong>100%</strong> số tiền đã thanh toán.</p>" : "")}");

        return MapToDetail(updated);
    }

    private async Task TryExecuteVnpayRefundAsync(Booking booking)
    {
        if (booking.PaymentMethod != "vnpay")
            return;
        if (string.IsNullOrWhiteSpace(booking.VnpayTransactionNo))
        {
            _logger.LogWarning("VNPay refund skipped for booking {BookingId}: VnpayTransactionNo not stored", booking.Id);
            return;
        }
        if (booking.PaymentPaidAt is null)
            return;

        var transactionDate = booking.PaymentPaidAt.Value.ToOffset(TimeSpan.FromHours(7)).ToString("yyyyMMddHHmmss");
        var orderInfo       = $"Hoan tien booking {booking.Id}";
        var (success, msg)  = await _vnpay.RefundAsync(
            booking.PaymentTxnId!,
            booking.VnpayTransactionNo,
            booking.RefundAmount,
            orderInfo,
            transactionDate,
            "system",
            "127.0.0.1");

        if (!success)
        {
            _logger.LogError("VNPay refund FAILED for booking {BookingId}: {Message}", booking.Id, msg);
            booking.PaymentStatus = PaymentStatus.refund_failed;
            _uow.Bookings.Update(booking);
            await _uow.SaveChangesAsync();
            await _notifications.NotifyAdminsAsync(
                "refund_failed",
                "Hoàn tiền VNPay thất bại",
                $"Booking {booking.Id} · {booking.RefundAmount:N0} VND · {msg}",
                "booking",
                booking.Id);
            await _notifications.CreateAsync(
                booking.CustomerId,
                "refund_failed",
                "Hoàn tiền gặp sự cố",
                $"Hệ thống chưa thể hoàn {booking.RefundAmount:N0} VND cho đơn #{booking.Id.ToString()[..8].ToUpper()}. Đội hỗ trợ sẽ xử lý thủ công trong 3–5 ngày làm việc.",
                "booking",
                booking.Id);
        }
        else
        {
            _logger.LogInformation("VNPay refund OK for booking {BookingId}", booking.Id);
        }
    }

    private static BookingListItemResponse MapToListItem(Booking b, bool hasReview = false) => new()
    {
        Id                = b.Id,
        TourId            = b.TourId,
        TourTitle         = b.Tour.Title,
        TourCoverImageUrl = b.Tour.CoverImageUrl,
        TourImages        = b.Tour.Images ?? [],
        TourDate          = b.TourDate,
        NumPeople         = b.NumPeople,
        TotalPrice        = b.TotalPrice,
        Status            = b.Status.ToString(),
        PaymentStatus     = b.PaymentStatus.ToString(),
        CreatedAt         = b.CreatedAt,
        HasReview         = hasReview,
    };

    private static BookingDetailResponse MapToDetail(Booking b) => new()
    {
        Id                = b.Id,
        TourId            = b.TourId,
        TourTitle         = b.Tour.Title,
        TourCoverImageUrl = b.Tour.CoverImageUrl,
        TourImages        = b.Tour.Images ?? [],
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
        ConversationId    = b.Conversation?.Id,
    };
}
