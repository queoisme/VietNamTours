using FluentValidation;

namespace GuideMarket.Api.DTOs.Requests;

public class CreateWithdrawalRequest
{
    public decimal Amount { get; set; }
    public string Method { get; set; } = default!;
    public string AccountInfo { get; set; } = default!;
    public string? Note { get; set; }
}

public class ProcessWithdrawalRequest
{
    public string? AdminNote { get; set; }
}

public class CreateWithdrawalRequestValidator : AbstractValidator<CreateWithdrawalRequest>
{
    public CreateWithdrawalRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(100_000).WithMessage("Minimum withdrawal is 100,000 VND");
        RuleFor(x => x.Method).NotEmpty()
            .Must(m => new[] { "bank", "momo", "zalopay", "vnpay" }.Contains(m))
            .WithMessage("Method must be bank, momo, zalopay, or vnpay");
        RuleFor(x => x.AccountInfo).NotEmpty();
    }
}
