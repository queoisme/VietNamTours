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
    private readonly int _timeoutMs;

    public SmtpEmailService(IConfiguration config)
    {
        _host = Environment.GetEnvironmentVariable("SMTP_HOST")
                ?? Environment.GetEnvironmentVariable("Smtp__Host")
                ?? config["Smtp:Host"]
                ?? throw new InvalidOperationException("Missing Smtp:Host");

        var portStr = Environment.GetEnvironmentVariable("SMTP_PORT")
                      ?? Environment.GetEnvironmentVariable("Smtp__Port")
                      ?? config["Smtp:Port"];
        if (!int.TryParse(portStr, out _port) || _port <= 0)
            _port = 587;

        _username = Environment.GetEnvironmentVariable("SMTP_USERNAME")
                    ?? Environment.GetEnvironmentVariable("Smtp__Username")
                    ?? config["Smtp:Username"];
        _password = Environment.GetEnvironmentVariable("SMTP_PASSWORD")
                    ?? Environment.GetEnvironmentVariable("Smtp__Password")
                    ?? config["Smtp:Password"];

        _fromEmail = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL")
                     ?? Environment.GetEnvironmentVariable("Smtp__FromEmail")
                     ?? config["Smtp:FromEmail"]
                     ?? throw new InvalidOperationException("Missing Smtp:FromEmail");
        _fromName = Environment.GetEnvironmentVariable("SMTP_FROM_NAME")
                    ?? Environment.GetEnvironmentVariable("Smtp__FromName")
                    ?? config["Smtp:FromName"]
                    ?? "GuideMarket";

        var timeoutStr = Environment.GetEnvironmentVariable("SMTP_TIMEOUT_MS")
                         ?? Environment.GetEnvironmentVariable("Smtp__TimeoutMs")
                         ?? config["Smtp:TimeoutMs"];
        if (!int.TryParse(timeoutStr, out _timeoutMs) || _timeoutMs < 1000)
            _timeoutMs = 10000;

        // Default: STARTTLS for port 587, SSL for 465.
        // Can override with Smtp:Security = None|StartTls|Ssl|Auto
        var security = (Environment.GetEnvironmentVariable("SMTP_SECURITY")
                        ?? Environment.GetEnvironmentVariable("Smtp__Security")
                        ?? config["Smtp:Security"]
                        ?? "Auto").Trim();
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
        client.Timeout = _timeoutMs;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_timeoutMs));

        try
        {
            await client.ConnectAsync(_host, _port, _secureSocketOptions, cts.Token);

            if (!string.IsNullOrWhiteSpace(_username))
            {
                if (string.IsNullOrWhiteSpace(_password))
                    throw new InvalidOperationException("Missing Smtp:Password");

                await client.AuthenticateAsync(_username, _password, cts.Token);
            }

            await client.SendAsync(message, cts.Token);
            await client.DisconnectAsync(true, cts.Token);
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException($"SMTP send timeout after {_timeoutMs} ms.", ex);
        }
    }
}
