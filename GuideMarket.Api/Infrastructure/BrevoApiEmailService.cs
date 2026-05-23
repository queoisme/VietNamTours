using System.Text;
using System.Text.Json;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Infrastructure;

public class BrevoApiEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly int _timeoutMs;

    public BrevoApiEmailService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _http = httpClientFactory.CreateClient();

        _apiKey = Environment.GetEnvironmentVariable("BREVO_API_KEY")
            ?? config["Brevo:ApiKey"]
            ?? throw new InvalidOperationException("Missing BREVO_API_KEY");

        _fromEmail = Environment.GetEnvironmentVariable("BREVO_FROM_EMAIL")
            ?? config["Brevo:FromEmail"]
            ?? throw new InvalidOperationException("Missing BREVO_FROM_EMAIL");

        _fromName = Environment.GetEnvironmentVariable("BREVO_FROM_NAME")
            ?? config["Brevo:FromName"]
            ?? "GuideMarket";

        var timeoutStr = Environment.GetEnvironmentVariable("BREVO_TIMEOUT_MS")
            ?? config["Brevo:TimeoutMs"];
        if (!int.TryParse(timeoutStr, out _timeoutMs) || _timeoutMs < 1000)
            _timeoutMs = 10000;
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var payload = new
        {
            sender = new
            {
                name = _fromName,
                email = _fromEmail
            },
            to = new[]
            {
                new { email = to }
            },
            subject,
            htmlContent = htmlBody
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        req.Headers.Add("accept", "application/json");
        req.Headers.Add("api-key", _apiKey);
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_timeoutMs));

        try
        {
            var res = await _http.SendAsync(req, cts.Token);
            var body = await res.Content.ReadAsStringAsync(cts.Token);
            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"Brevo API error {(int)res.StatusCode}: {body}");
        }
        catch (OperationCanceledException ex) when (cts.IsCancellationRequested)
        {
            throw new TimeoutException($"Brevo API send timeout after {_timeoutMs} ms.", ex);
        }
    }
}
