using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.BackgroundJobs;

public class SendEmailJob
{
    private readonly IEmailService _email;
    private readonly ILogger<SendEmailJob> _logger;

    public SendEmailJob(IEmailService email, ILogger<SendEmailJob> logger)
    {
        _email  = email;
        _logger = logger;
    }

    public async Task ExecuteAsync(string to, string subject, string htmlBody)
    {
        try
        {
            await _email.SendAsync(to, subject, htmlBody);
            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}: {Subject}", to, subject);
            throw; // Hangfire will retry
        }
    }
}
