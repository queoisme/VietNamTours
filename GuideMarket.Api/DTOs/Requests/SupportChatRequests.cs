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
    public string Content { get; set; } = string.Empty;
    public List<GuideMarket.Api.DTOs.Responses.MessageAttachmentDto>? Attachments { get; set; }
}

public class SendSupportMessageRequestValidator : AbstractValidator<SendSupportMessageRequest>
{
    public SendSupportMessageRequestValidator()
    {
        RuleFor(x => x.Content).MaximumLength(5000);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Content) || x.Attachments?.Count > 0)
            .WithMessage("Message must have content or at least one attachment");

        RuleFor(x => x.Attachments)
            .Must(a => a == null || a.Count <= 5)
            .WithMessage("Maximum 5 attachments per message");
    }
}

public class UpdateSupportStatusRequest
{
    public string Status { get; set; } = default!;
}
