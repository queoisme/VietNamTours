using FluentValidation;

namespace GuideMarket.Api.DTOs.Requests;

public class CreateGuideApplicationRequest
{
    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Location { get; set; }
    public string Bio { get; set; } = default!;
    public short ExperienceYears { get; set; }
    public string[] Languages { get; set; } = [];
    public List<CertificationItemRequest> Certifications { get; set; } = [];
    public string IdentityDocUrl { get; set; } = default!;
    public string[] CertificateUrls { get; set; } = [];
}

public class RejectApplicationRequest
{
    public string RejectionReason { get; set; } = default!;
}

public class GuideApplicationListParams
{
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int Size { get; set; } = 20;
}

public class CreateGuideApplicationRequestValidator : AbstractValidator<CreateGuideApplicationRequest>
{
    public CreateGuideApplicationRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Bio).NotEmpty();
        RuleFor(x => x.ExperienceYears).GreaterThanOrEqualTo((short)0);
        RuleFor(x => x.Languages).NotEmpty().WithMessage("At least one language is required");
        RuleFor(x => x.IdentityDocUrl).NotEmpty().WithMessage("Identity document URL is required");
    }
}

public class RejectApplicationRequestValidator : AbstractValidator<RejectApplicationRequest>
{
    public RejectApplicationRequestValidator()
    {
        RuleFor(x => x.RejectionReason).NotEmpty().MaximumLength(500);
    }
}
