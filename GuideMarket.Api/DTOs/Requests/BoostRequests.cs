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

public class UpdateBoostPlanRequest
{
    public decimal? Price { get; set; }
    public int? Days { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateBoostPlanRequestValidator : AbstractValidator<UpdateBoostPlanRequest>
{
    public UpdateBoostPlanRequestValidator()
    {
        When(x => x.Price.HasValue, () =>
            RuleFor(x => x.Price!.Value).GreaterThan(0).WithMessage("Giá phải lớn hơn 0"));
        When(x => x.Days.HasValue, () =>
            RuleFor(x => x.Days!.Value).GreaterThan(0).WithMessage("Số ngày phải lớn hơn 0"));
        When(x => x.Description is not null, () =>
            RuleFor(x => x.Description!).MaximumLength(500));
    }
}

public class UpdateSubscriptionPlanRequest
{
    public decimal? Price { get; set; }
    public int? Days { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}

public class UpdateSubscriptionPlanRequestValidator : AbstractValidator<UpdateSubscriptionPlanRequest>
{
    public UpdateSubscriptionPlanRequestValidator()
    {
        When(x => x.Price.HasValue, () =>
            RuleFor(x => x.Price!.Value).GreaterThan(0).WithMessage("Giá phải lớn hơn 0"));
        When(x => x.Days.HasValue, () =>
            RuleFor(x => x.Days!.Value).GreaterThan(0).WithMessage("Số ngày phải lớn hơn 0"));
        When(x => x.Description is not null, () =>
            RuleFor(x => x.Description!).MaximumLength(500));
    }
}
