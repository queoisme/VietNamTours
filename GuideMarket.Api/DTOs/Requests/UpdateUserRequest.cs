using FluentValidation;
using Microsoft.AspNetCore.Http;

namespace GuideMarket.Api.DTOs.Requests;

/// <summary>Wrapper required by Swashbuckle for IFormFile in multipart/form-data.</summary>
public class AvatarUploadRequest
{
    public IFormFile? File { get; set; }
}

public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public string? Phone { get; set; }
}

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        When(x => x.FullName != null, () =>
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(150));

        When(x => x.Phone != null, () =>
            RuleFor(x => x.Phone).Matches(@"^\+?[\d\s\-]{7,20}$").WithMessage("Invalid phone format"));
    }
}
