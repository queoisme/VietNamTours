using System.Text;
using System.Text.Json;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Infrastructure;

public class ResendEmailService : IEmailService
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _fromEmail;
    private readonly string _fromName;

    public ResendEmailService(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _http      = httpClientFactory.CreateClient();
        _apiKey    = config["Resend:ApiKey"]!;
        _fromEmail = config["Resend:FromEmail"] ?? "onboarding@resend.dev";
        _fromName  = config["Resend:FromName"]  ?? "GuideMarket";
    }

    public async Task SendAsync(string to, string subject, string htmlBody)
    {
        var payload = new
        {
            from    = $"{_fromName} <{_fromEmail}>",
            to      = new[] { to },
            subject,
            html    = htmlBody,
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        req.Headers.Add("Authorization", $"Bearer {_apiKey}");
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Resend error: {body}");
        }
    }
}
