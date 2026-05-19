using FluentValidation;

namespace GuideMarket.Api.DTOs.Requests;

public class SendMessageRequest
{
    public string Content { get; set; } = default!;
}

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(5000);
    }
}
