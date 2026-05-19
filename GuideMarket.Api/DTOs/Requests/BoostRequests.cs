using FluentValidation;

namespace GuideMarket.Api.DTOs.Requests;

public class CreateBoostRequest
{
    public Guid TourId { get; set; }
    public string Plan { get; set; } = default!;
}

public class CreateSubscriptionRequest
{
    public string Plan { get; set; } = default!;
}

public class CreateBoostRequestValidator : AbstractValidator<CreateBoostRequest>
{
    public CreateBoostRequestValidator()
    {
        RuleFor(x => x.TourId).NotEmpty();
        RuleFor(x => x.Plan).NotEmpty().Must(p => new[] { "basic", "standard", "premium" }.Contains(p))
            .WithMessage("Plan must be basic, standard, or premium");
    }
}

public class CreateSubscriptionRequestValidator : AbstractValidator<CreateSubscriptionRequest>
{
    public CreateSubscriptionRequestValidator()
    {
        RuleFor(x => x.Plan).NotEmpty().Must(p => new[] { "premium", "pro" }.Contains(p))
            .WithMessage("Plan must be premium or pro");
    }
}
