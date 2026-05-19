using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GuideMarket.Api.Infrastructure;

/// <summary>
/// HTTP wrapper for Supabase Auth REST API.
/// Uses Admin endpoints (service role key) for user management.
/// Uses public endpoints (anon key) for sign-in / token operations.
/// </summary>
public class SupabaseAuthClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _anonKey;
    private readonly string _serviceRoleKey;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public SupabaseAuthClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        _baseUrl = config["Supabase:Url"]!.TrimEnd('/') + "/auth/v1";
        _anonKey = config["Supabase:AnonKey"]!;
        _serviceRoleKey = config["Supabase:ServiceRoleKey"]!;
    }

    // ----------------------------------------------------------------
    // Register — Admin API (service role key)
    // ----------------------------------------------------------------
    public async Task<SupabaseUserDto> AdminCreateUserAsync(
        string email, string password, string fullName)
    {
        var body = new
        {
            email,
            password,
            email_confirm = true,           // auto-confirm; email verification handled by Supabase Dashboard setting
            user_metadata = new { full_name = fullName }
        };

        var req = BuildRequest(HttpMethod.Post, "/admin/users", body, useServiceRole: true);
        var res = await _http.SendAsync(req);
        var content = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(ExtractError(content));

        return JsonSerializer.Deserialize<SupabaseUserDto>(content, JsonOpts)!;
    }

    // ----------------------------------------------------------------
    // Register — public signup (sends confirmation email)
    // ----------------------------------------------------------------
    public async Task SignUpAsync(string email, string password, string fullName)
    {
        var body = new { email, password, data = new { full_name = fullName } };
        var req = BuildRequest(HttpMethod.Post, "/signup", body);
        var res = await _http.SendAsync(req);
        var content = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Supabase signup failed ({(int)res.StatusCode}): {ExtractError(content)}");
    }

    // ----------------------------------------------------------------
    // Login — public endpoint (anon key)
    // ----------------------------------------------------------------
    public async Task<SupabaseTokenResponse> SignInWithPasswordAsync(
        string email, string password)
    {
        var body = new { email, password };
        var req = BuildRequest(HttpMethod.Post, "/token?grant_type=password", body);
        var res = await _http.SendAsync(req);
        var content = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new UnauthorizedAccessException(ExtractError(content));

        return JsonSerializer.Deserialize<SupabaseTokenResponse>(content, JsonOpts)!;
    }

    // ----------------------------------------------------------------
    // Verify email OTP (confirm signup via 6-digit code)
    // ----------------------------------------------------------------
    public async Task<SupabaseTokenResponse> VerifyEmailOtpAsync(string email, string token)
    {
        var body = new { type = "signup", email, token };
        var req = BuildRequest(HttpMethod.Post, "/verify", body);
        var res = await _http.SendAsync(req);
        var content = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException(ExtractError(content));

        return JsonSerializer.Deserialize<SupabaseTokenResponse>(content, JsonOpts)!;
    }

    // ----------------------------------------------------------------
    // Resend signup email / OTP
    // ----------------------------------------------------------------
    public async Task ResendSignupEmailAsync(string email)
    {
        var body = new { type = "signup", email };
        var req = BuildRequest(HttpMethod.Post, "/resend", body);
        var res = await _http.SendAsync(req);
        var content = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Supabase resend failed ({(int)res.StatusCode}): {ExtractError(content)}");
    }

    // ----------------------------------------------------------------
    // Refresh token
    // ----------------------------------------------------------------
    public async Task<SupabaseTokenResponse> RefreshTokenAsync(string refreshToken)
    {
        var body = new { refresh_token = refreshToken };
        var req = BuildRequest(HttpMethod.Post, "/token?grant_type=refresh_token", body);
        var res = await _http.SendAsync(req);
        var content = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new UnauthorizedAccessException(ExtractError(content));

        return JsonSerializer.Deserialize<SupabaseTokenResponse>(content, JsonOpts)!;
    }

    // ----------------------------------------------------------------
    // Logout
    // ----------------------------------------------------------------
    public async Task SignOutAsync(string accessToken)
    {
        var req = BuildRequest(HttpMethod.Post, "/logout", body: null);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await _http.SendAsync(req);
    }

    // ----------------------------------------------------------------
    // Forgot password — gửi email reset
    // ----------------------------------------------------------------
    public async Task SendPasswordRecoveryAsync(string email)
    {
        var body = new { email };
        var req = BuildRequest(HttpMethod.Post, "/recover", body);
        await _http.SendAsync(req);   // Luôn trả 200 để tránh email enumeration
    }

    // ----------------------------------------------------------------
    // Reset password — dùng access token từ link email
    // ----------------------------------------------------------------
    public async Task UpdatePasswordAsync(string accessToken, string newPassword)
    {
        var body = new { password = newPassword };
        var req = BuildRequest(HttpMethod.Put, "/user", body);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var res = await _http.SendAsync(req);

        if (!res.IsSuccessStatusCode)
        {
            var content = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException(ExtractError(content));
        }
    }

    // ----------------------------------------------------------------
    // Phone OTP — gửi OTP qua SMS
    // ----------------------------------------------------------------
    public async Task SendPhoneOtpAsync(string phone)
    {
        var body = new { phone, channel = "sms" };
        var req = BuildRequest(HttpMethod.Post, "/otp", body);
        var res = await _http.SendAsync(req);

        if (!res.IsSuccessStatusCode)
        {
            var content = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException(ExtractError(content));
        }
    }

    // ----------------------------------------------------------------
    // Phone OTP — xác minh OTP
    // ----------------------------------------------------------------
    public async Task VerifyPhoneOtpAsync(string phone, string token)
    {
        var body = new { phone, token, type = "sms" };
        var req = BuildRequest(HttpMethod.Post, "/verify", body);
        var res = await _http.SendAsync(req);

        if (!res.IsSuccessStatusCode)
        {
            var content = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException(ExtractError(content));
        }
    }

    // ----------------------------------------------------------------
    // Admin: update app_metadata.role (used after guide application approval)
    // ----------------------------------------------------------------
    public async Task AdminUpdateUserRoleAsync(Guid userId, string role)
    {
        var body = new { app_metadata = new { role } };
        var req = BuildRequest(HttpMethod.Put, $"/admin/users/{userId}", body, useServiceRole: true);
        var res = await _http.SendAsync(req);

        if (!res.IsSuccessStatusCode)
        {
            var content = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException(ExtractError(content));
        }
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------
    private HttpRequestMessage BuildRequest(
        HttpMethod method, string path, object? body, bool useServiceRole = false)
    {
        var req = new HttpRequestMessage(method, _baseUrl + path);
        var key = useServiceRole ? _serviceRoleKey : _anonKey;

        req.Headers.Add("apikey", key);
        if (useServiceRole)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        if (body is not null)
            req.Content = new StringContent(
                JsonSerializer.Serialize(body, JsonOpts),
                Encoding.UTF8, "application/json");

        return req;
    }

    private static string ExtractError(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("msg", out var msg)) return msg.GetString()!;
            if (doc.RootElement.TryGetProperty("message", out var message)) return message.GetString()!;
            if (doc.RootElement.TryGetProperty("error_description", out var desc)) return desc.GetString()!;
        }
        catch { }
        return "Authentication error";
    }
}

// ----------------------------------------------------------------
// DTOs for Supabase API responses
// ----------------------------------------------------------------

public class SupabaseTokenResponse
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public int ExpiresIn { get; set; }
    public string TokenType { get; set; } = default!;
    public SupabaseUserDto? User { get; set; }
}

public class SupabaseUserDto
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public Dictionary<string, JsonElement>? UserMetadata { get; set; }
    public string? EmailConfirmedAt { get; set; }
}
