using FluentValidation;

namespace GuideMarket.Api.DTOs.Requests;

public class CreateSupportTicketRequest
{
    public string Subject { get; set; } = default!;
}

public class CreateSupportTicketRequestValidator : AbstractValidator<CreateSupportTicketRequest>
{
    public CreateSupportTicketRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
    }
}

public class SendSupportMessageRequest
{
    public string Content { get; set; } = default!;
}

public class SendSupportMessageRequestValidator : AbstractValidator<SendSupportMessageRequest>
{
    public SendSupportMessageRequestValidator()
    {
        RuleFor(x => x.Content).NotEmpty().MaximumLength(5000);
    }
}

public class UpdateSupportStatusRequest
{
    public string Status { get; set; } = default!;
}
