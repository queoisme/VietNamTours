using FluentValidation;

namespace GuideMarket.Api.DTOs.Requests;

public class RegisterRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FullName { get; set; } = default!;
}

public class LoginRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = default!;
}

public class ForgotPasswordRequest
{
    public string Email { get; set; } = default!;
}

public class ResetPasswordRequest
{
    public string AccessToken { get; set; } = default!;
    public string NewPassword { get; set; } = default!;
}

public class RequestOtpRequest
{
    public string Phone { get; set; } = default!;
}

public class VerifyPhoneRequest
{
    public string Phone { get; set; } = default!;
    public string Token { get; set; } = default!;
}

public class VerifyEmailRequest
{
    public string Email { get; set; } = default!;
    public string Otp { get; set; } = default!;
}

// --- Validators ---

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(255);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(72)
            .WithMessage("Password must be 8–72 characters");
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.AccessToken).NotEmpty();
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(72);
    }
}

public class RequestOtpRequestValidator : AbstractValidator<RequestOtpRequest>
{
    public RequestOtpRequestValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
    }
}

public class VerifyPhoneRequestValidator : AbstractValidator<VerifyPhoneRequest>
{
    public VerifyPhoneRequestValidator()
    {
        RuleFor(x => x.Phone).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Token).NotEmpty().Length(6).WithMessage("OTP token must be 6 digits");
    }
}

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Otp).NotEmpty().Matches(@"^\d{6,8}$").WithMessage("OTP must be 6-8 digits");
    }
}
