using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GuideMarket.Api.Infrastructure;

public class MomoClient
{
    private readonly string     _partnerCode;
    private readonly string     _accessKey;
    private readonly string     _secretKey;
    private readonly string     _endpoint;
    private readonly string     _ipnUrl;
    private readonly string     _redirectUrl;
    private readonly string     _frontendBaseUrl;
    private readonly HttpClient _http = new();

    public MomoClient(IConfiguration config)
    {
        _partnerCode     = config["Momo:PartnerCode"]!;
        _accessKey       = config["Momo:AccessKey"]!;
        _secretKey       = config["Momo:SecretKey"]!;
        _endpoint        = config["Momo:Endpoint"]!;
        _ipnUrl          = config["Momo:IpnUrl"]!;
        _redirectUrl     = config["Momo:RedirectUrl"]!;
        _frontendBaseUrl = config["Momo:FrontendBaseUrl"] ?? "http://localhost:3000";
    }

    public async Task<(string PayUrl, string QrCodeUrl)> CreatePaymentAsync(
        string orderId, decimal amount, string orderInfo)
    {
        var requestId  = orderId;
        var amountLong = (long)amount;

        var rawSignature =
            $"accessKey={_accessKey}" +
            $"&amount={amountLong}" +
            $"&extraData=" +
            $"&ipnUrl={_ipnUrl}" +
            $"&orderId={orderId}" +
            $"&orderInfo={orderInfo}" +
            $"&partnerCode={_partnerCode}" +
            $"&redirectUrl={_redirectUrl}" +
            $"&requestId={requestId}" +
            $"&requestType=captureWallet";

        var signature = HmacSha256(_secretKey, rawSignature);

        var body = new
        {
            partnerCode = _partnerCode,
            requestType = "captureWallet",
            ipnUrl      = _ipnUrl,
            redirectUrl = _redirectUrl,
            orderId,
            amount      = amountLong,
            orderInfo,
            requestId,
            extraData   = "",
            lang        = "vi",
            signature,
        };

        var response = await _http.PostAsJsonAsync(_endpoint, body);
        response.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root      = doc.RootElement;
        var payUrl    = root.TryGetProperty("payUrl",    out var p) ? p.GetString() ?? "" : "";
        var qrCodeUrl = root.TryGetProperty("qrCodeUrl", out var q) ? q.GetString() ?? "" : "";

        return (payUrl, qrCodeUrl);
    }

    public bool VerifyIpn(MomoIpnPayload ipn)
    {
        var rawSignature =
            $"accessKey={_accessKey}" +
            $"&amount={ipn.Amount}" +
            $"&extraData={ipn.ExtraData}" +
            $"&message={ipn.Message}" +
            $"&orderId={ipn.OrderId}" +
            $"&orderInfo={ipn.OrderInfo}" +
            $"&orderType={ipn.OrderType}" +
            $"&partnerCode={ipn.PartnerCode}" +
            $"&payType={ipn.PayType}" +
            $"&requestId={ipn.RequestId}" +
            $"&responseTime={ipn.ResponseTime}" +
            $"&resultCode={ipn.ResultCode}" +
            $"&transId={ipn.TransId}";

        var expected = HmacSha256(_secretKey, rawSignature);
        return string.Equals(expected, ipn.Signature, StringComparison.OrdinalIgnoreCase);
    }

    public string GetFrontendSuccessUrl(string orderId) =>
        $"{_frontendBaseUrl}/payment/success?txnRef={orderId}";

    public string GetFrontendFailedUrl(string resultCode) =>
        $"{_frontendBaseUrl}/payment/failed?code={resultCode}";

    private static string HmacSha256(string key, string data)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLower();
    }
}

public class MomoIpnPayload
{
    [JsonPropertyName("partnerCode")]  public string PartnerCode  { get; set; } = "";
    [JsonPropertyName("orderId")]      public string OrderId      { get; set; } = "";
    [JsonPropertyName("requestId")]    public string RequestId    { get; set; } = "";
    [JsonPropertyName("amount")]       public long   Amount       { get; set; }
    [JsonPropertyName("orderInfo")]    public string OrderInfo    { get; set; } = "";
    [JsonPropertyName("orderType")]    public string OrderType    { get; set; } = "";
    [JsonPropertyName("transId")]      public long   TransId      { get; set; }
    [JsonPropertyName("resultCode")]   public int    ResultCode   { get; set; }
    [JsonPropertyName("message")]      public string Message      { get; set; } = "";
    [JsonPropertyName("payType")]      public string PayType      { get; set; } = "";
    [JsonPropertyName("responseTime")] public long   ResponseTime { get; set; }
    [JsonPropertyName("extraData")]    public string ExtraData    { get; set; } = "";
    [JsonPropertyName("signature")]    public string Signature    { get; set; } = "";
}
