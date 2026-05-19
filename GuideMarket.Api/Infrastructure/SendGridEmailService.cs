using GuideMarket.Api.Services.Interfaces;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace GuideMarket.Api.Infrastructure;

public class SendGridEmailService : IEmailService
{
    private readonly SendGridClient _client;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public SendGridEmailService(IConfiguration config)
    {
        _client    = new SendGridClient(config["SendGrid:ApiKey"]);
        _fromEmail = config["SendGrid:FromEmail"] ?? "noreply@guidemarket.vn";
        _fromName  = config["SendGrid:FromName"]  ?? "GuideMarket";
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var msg = new SendGridMessage
        {
            From        = new EmailAddress(_fromEmail, _fromName),
            Subject     = subject,
            HtmlContent = htmlBody,
        };
        msg.AddTo(new EmailAddress(to));
        await _client.SendEmailAsync(msg);
    }
}
