using GuideMarket.Api.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GuideMarket.Api.Infrastructure;

public class SmtpEmailService : IEmailService
{
    private readonly string _host;
    private readonly int _port;
    private readonly string? _username;
    private readonly string? _password;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly SecureSocketOptions _secureSocketOptions;

    public SmtpEmailService(IConfiguration config)
    {
        _host = config["Smtp:Host"] ?? throw new InvalidOperationException("Missing Smtp:Host");

        if (!int.TryParse(config["Smtp:Port"], out _port) || _port <= 0)
            _port = 587;

        _username = config["Smtp:Username"];
        _password = config["Smtp:Password"];

        _fromEmail = config["Smtp:FromEmail"] ?? throw new InvalidOperationException("Missing Smtp:FromEmail");
        _fromName = config["Smtp:FromName"] ?? "GuideMarket";

        // Default: STARTTLS for port 587, SSL for 465.
        // Can override with Smtp:Security = None|StartTls|Ssl|Auto
        var security = (config["Smtp:Security"] ?? "Auto").Trim();
        _secureSocketOptions = security.ToLowerInvariant() switch
        {
            "none" => SecureSocketOptions.None,
            "starttls" => SecureSocketOptions.StartTls,
            "ssl" => SecureSocketOptions.SslOnConnect,
            _ => _port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls
        };
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_fromName, _fromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(_host, _port, _secureSocketOptions);

        if (!string.IsNullOrWhiteSpace(_username))
        {
            if (string.IsNullOrWhiteSpace(_password))
                throw new InvalidOperationException("Missing Smtp:Password");

            await client.AuthenticateAsync(_username, _password);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }
}
