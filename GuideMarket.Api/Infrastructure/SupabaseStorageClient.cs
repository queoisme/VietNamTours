using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace GuideMarket.Api.Infrastructure;

/// <summary>
/// HTTP wrapper for Supabase Storage REST API.
/// Uses service role key for all operations (server-side only).
/// </summary>
public class SupabaseStorageClient
{
    private readonly HttpClient _http;
    private readonly string _storageBaseUrl;
    private readonly string _publicBaseUrl;
    private readonly string _serviceRoleKey;

    public SupabaseStorageClient(HttpClient http, IConfiguration config)
    {
        _http = http;
        var supabaseUrl = config["Supabase:Url"]!.TrimEnd('/');
        _storageBaseUrl = supabaseUrl + "/storage/v1";
        _publicBaseUrl = supabaseUrl + "/storage/v1/object/public";
        _serviceRoleKey = config["Supabase:ServiceRoleKey"]!;
    }

    /// <summary>
    /// Upload file lên public bucket. Trả về public URL.
    /// </summary>
    public async Task<string> UploadPublicAsync(
        string bucket, string path, Stream content, string contentType)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{_storageBaseUrl}/object/{bucket}/{path}");
        req.Headers.Add("apikey", _serviceRoleKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        req.Headers.Add("x-upsert", "true");

        req.Content = new StreamContent(content);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Storage upload failed: {ExtractError(err)}");
        }

        return GetPublicUrl(bucket, path);
    }

    /// <summary>
    /// Upload file lên private bucket. Trả về path (dùng để tạo signed URL sau).
    /// </summary>
    public async Task<string> UploadPrivateAsync(
        string bucket, string path, Stream content, string contentType)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{_storageBaseUrl}/object/{bucket}/{path}");
        req.Headers.Add("apikey", _serviceRoleKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        req.Headers.Add("x-upsert", "true");

        req.Content = new StreamContent(content);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var err = await res.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Storage upload failed: {ExtractError(err)}");
        }

        return $"{bucket}/{path}";
    }

    /// <summary>
    /// Tạo signed URL cho private bucket (TTL mặc định 15 phút).
    /// </summary>
    public async Task<string> CreateSignedUrlAsync(string bucket, string path, int expiresIn = 900)
    {
        var body = JsonSerializer.Serialize(new { expiresIn });
        var req = new HttpRequestMessage(HttpMethod.Post, $"{_storageBaseUrl}/object/sign/{bucket}/{path}");
        req.Headers.Add("apikey", _serviceRoleKey);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _serviceRoleKey);
        req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        var res = await _http.SendAsync(req);
        var content = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new InvalidOperationException($"Failed to create signed URL: {ExtractError(content)}");

        var doc = JsonDocument.Parse(content);
        if (doc.RootElement.TryGetProperty("signedURL", out var signedUrl))
        {
            var relativePath = signedUrl.GetString()!;
            // signedURL là relative path — prepend supabase base URL
            return relativePath.StartsWith("http") ? relativePath
                : _storageBaseUrl.Replace("/storage/v1", "") + relativePath;
        }

        throw new InvalidOperationException("Signed URL not found in response");
    }

    /// <summary>Public URL cho public bucket.</summary>
    public string GetPublicUrl(string bucket, string path) => $"{_publicBaseUrl}/{bucket}/{path}";

    private static string ExtractError(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("message", out var m)) return m.GetString()!;
            if (doc.RootElement.TryGetProperty("error", out var e)) return e.GetString()!;
        }
        catch { }
        return "Unknown storage error";
    }
}
