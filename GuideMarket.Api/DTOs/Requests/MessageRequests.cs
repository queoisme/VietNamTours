using FluentValidation;
using GuideMarket.Api.DTOs.Responses;

namespace GuideMarket.Api.DTOs.Requests;

public class SendMessageRequest
{
    public string Content { get; set; } = string.Empty;
    public List<MessageAttachmentDto>? Attachments { get; set; }
}

public class SendMessageRequestValidator : AbstractValidator<SendMessageRequest>
{
    public SendMessageRequestValidator()
    {
        RuleFor(x => x.Content).MaximumLength(5000);

        // Must have text content OR at least one attachment
        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Content) || x.Attachments?.Count > 0)
            .WithMessage("Message must have content or at least one attachment");

        RuleFor(x => x.Attachments)
            .Must(a => a == null || a.Count <= 5)
            .WithMessage("Maximum 5 attachments per message");
    }
}
