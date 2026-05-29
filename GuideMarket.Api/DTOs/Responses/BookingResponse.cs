namespace GuideMarket.Api.DTOs.Responses;

public class BookingListItemResponse
{
    public Guid Id { get; set; }
    public Guid TourId { get; set; }
    public string TourTitle { get; set; } = default!;
    public string? TourCoverImageUrl { get; set; }
    public string[] TourImages { get; set; } = [];
    public DateOnly TourDate { get; set; }
    public short NumPeople { get; set; }
    public decimal TotalPrice { get; set; }
    public string Status { get; set; } = default!;
    public string PaymentStatus { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
    public bool HasReview { get; set; }
}

public class BookingDetailResponse
{
    public Guid Id { get; set; }
    public Guid TourId { get; set; }
    public string TourTitle { get; set; } = default!;
    public string? TourCoverImageUrl { get; set; }
    public string[] TourImages { get; set; } = [];
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public Guid GuideId { get; set; }
    public string GuideName { get; set; } = default!;
    public DateOnly TourDate { get; set; }
    public short NumPeople { get; set; }
    public decimal TotalPrice { get; set; }
    public string ContactName { get; set; } = default!;
    public string ContactPhone { get; set; } = default!;
    public string? ContactEmail { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = default!;
    public string? RejectionReason { get; set; }
    public string? CancellationBy { get; set; }
    public string? CancellationReason { get; set; }
    public decimal RefundAmount { get; set; }
    public string? RefundPolicy { get; set; }
    public string PaymentStatus { get; set; } = default!;
    public string? PaymentMethod { get; set; }
    public string? PaymentTxnId { get; set; }
    public DateTimeOffset? PaymentPaidAt { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? ConversationId { get; set; }
}

public class MomoPaymentResponse
{
    public string PayUrl    { get; set; } = default!;
    public string QrCodeUrl { get; set; } = default!;
}

public class VnPayPaymentResponse
{
    public string PayUrl { get; set; } = default!;
}
