using FluentValidation;

namespace GuideMarket.Api.DTOs.Requests;

public class CreateBookingRequest
{
    public Guid TourId { get; set; }
    public DateOnly TourDate { get; set; }
    public short NumPeople { get; set; }
    public short NumDays { get; set; } = 1;
    public string ContactName { get; set; } = default!;
    public string ContactPhone { get; set; } = default!;
    public string? ContactEmail { get; set; }
    public string? Note { get; set; }
}

public class RejectBookingRequest
{
    public string Reason { get; set; } = default!;
}

public class CancelBookingRequest
{
    public string? Reason { get; set; }
}

public class CreateMomoPaymentRequest
{
    public Guid BookingId { get; set; }
}

public class CreateVNPayPaymentRequest
{
    public Guid BookingId { get; set; }
}

public class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.TourId).NotEmpty();
        RuleFor(x => x.TourDate)
            .GreaterThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Tour date must be in the future");
        RuleFor(x => x.NumPeople).GreaterThan((short)0);
        RuleFor(x => x.NumDays).GreaterThan((short)0).LessThanOrEqualTo((short)30);
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.ContactPhone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.ContactEmail)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }
}

public class RejectBookingRequestValidator : AbstractValidator<RejectBookingRequest>
{
    public RejectBookingRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

public class CreateMomoPaymentRequestValidator : AbstractValidator<CreateMomoPaymentRequest>
{
    public CreateMomoPaymentRequestValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
    }
}
