using FluentValidation;

namespace GuideMarket.Api.DTOs.Requests;

public class CreateReviewRequest
{
    public Guid BookingId { get; set; }
    public short Rating { get; set; }
    public string? Comment { get; set; }
}

public class ReplyReviewRequest
{
    public string Reply { get; set; } = default!;
}

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.BookingId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween((short)1, (short)5);
        RuleFor(x => x.Comment).MaximumLength(2000).When(x => x.Comment != null);
    }
}

public class ReplyReviewRequestValidator : AbstractValidator<ReplyReviewRequest>
{
    public ReplyReviewRequestValidator()
    {
        RuleFor(x => x.Reply).NotEmpty().MaximumLength(2000);
    }
}
