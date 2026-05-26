using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace GuideMarket.Api.Infrastructure;

public class VnPayClient
{
    private readonly string _tmnCode;
    private readonly string _hashSecret;
    private readonly string _paymentUrl;
    private readonly string _returnUrl;
    private readonly string _frontendBaseUrl;

    public VnPayClient(IConfiguration config)
    {
        _tmnCode         = config["VnPay:TmnCode"]!;
        _hashSecret      = config["VnPay:HashSecret"]!;
        _paymentUrl      = config["VnPay:PaymentUrl"]!;
        _returnUrl       = config["VnPay:ReturnUrl"]!;
        _frontendBaseUrl = config["VnPay:FrontendBaseUrl"] ?? "http://localhost:5173";
    }

    /// <summary>
    /// Tạo URL thanh toán VNPay (build locally, không cần HTTP call).
    /// </summary>
    public string CreatePaymentUrl(string txnRef, decimal amount, string orderInfo, string clientIp)
    {
        var createDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
        var amountStr  = ((long)(amount * 100)).ToString();
        var ip         = string.IsNullOrWhiteSpace(clientIp) || clientIp == "::1"
                         ? "127.0.0.1" : clientIp;

        var vnpParams = new SortedDictionary<string, string>
        {
            ["vnp_Version"]    = "2.1.0",
            ["vnp_Command"]    = "pay",
            ["vnp_TmnCode"]    = _tmnCode,
            ["vnp_Amount"]     = amountStr,
            ["vnp_CreateDate"] = createDate,
            ["vnp_CurrCode"]   = "VND",
            ["vnp_IpAddr"]     = ip,
            ["vnp_Locale"]     = "vn",
            ["vnp_OrderInfo"]  = orderInfo,
            ["vnp_OrderType"]  = "other",
            ["vnp_ReturnUrl"]  = _returnUrl,
            ["vnp_TxnRef"]     = txnRef,
        };

        var rawData  = BuildRawData(vnpParams);
        var hash     = HmacSha512(_hashSecret, rawData);
        var query    = BuildQueryString(vnpParams);

        return $"{_paymentUrl}?{query}&vnp_SecureHash={hash}";
    }

    /// <summary>
    /// Xác minh chữ ký HMAC-SHA512 từ callback (return URL hoặc IPN).
    /// </summary>
    public bool VerifySignature(IQueryCollection query)
    {
        var secureHash = query["vnp_SecureHash"].ToString();
        if (string.IsNullOrEmpty(secureHash)) return false;

        var vnpParams = query
            .Where(kv => kv.Key.StartsWith("vnp_")
                      && kv.Key != "vnp_SecureHash"
                      && kv.Key != "vnp_SecureHashType")
            .OrderBy(kv => kv.Key)
            .ToDictionary(kv => kv.Key, kv => kv.Value.ToString());

        var rawData  = BuildRawData(new SortedDictionary<string, string>(vnpParams));
        var expected = HmacSha512(_hashSecret, rawData);

        return string.Equals(expected, secureHash, StringComparison.OrdinalIgnoreCase);
    }

    public string GetResponseCode(IQueryCollection query) =>
        query["vnp_ResponseCode"].ToString();

    public string GetTxnRef(IQueryCollection query) =>
        query["vnp_TxnRef"].ToString();

    public string GetFrontendSuccessUrl(string txnRef) =>
        $"{_frontendBaseUrl.TrimEnd('/')}/payment/success?txnRef={txnRef}";

    public string GetFrontendFailedUrl(string code) =>
        $"{_frontendBaseUrl.TrimEnd('/')}/payment/failed?code={code}";

    // --- Helpers ---

    // VNPay yêu cầu URL-encode giá trị (spaces → +) khi tạo chuỗi hash
    private static string BuildRawData(SortedDictionary<string, string> parms) =>
        string.Join("&", parms.Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}"));

    private static string BuildQueryString(SortedDictionary<string, string> parms) =>
        string.Join("&", parms.Select(kv => $"{kv.Key}={WebUtility.UrlEncode(kv.Value)}"));

    private static string HmacSha512(string key, string data)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLower();
    }
}
