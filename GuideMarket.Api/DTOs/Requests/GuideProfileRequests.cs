using FluentValidation;

namespace GuideMarket.Api.DTOs.Requests;

public class UpdateGuideProfileRequest
{
    public string? Bio { get; set; }
    public short? ExperienceYears { get; set; }
    public string[]? Languages { get; set; }
    public List<CertificationItemRequest>? Certifications { get; set; }
}

public class CertificationItemRequest
{
    public string Name { get; set; } = default!;
    public string IssuedBy { get; set; } = default!;
    public int Year { get; set; }
}

public class UpdateGuideProfileRequestValidator : AbstractValidator<UpdateGuideProfileRequest>
{
    public UpdateGuideProfileRequestValidator()
    {
        RuleFor(x => x.ExperienceYears).GreaterThanOrEqualTo((short)0).When(x => x.ExperienceYears.HasValue);
        RuleFor(x => x.Languages).Must(l => l!.Length > 0).When(x => x.Languages is not null)
            .WithMessage("Languages list cannot be empty");
    }
}
