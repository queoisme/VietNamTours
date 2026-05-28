using System.Net;
using System.Net.Http.Json;
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
    private readonly string _refundUrl;
    private readonly HttpClient _http = new();

    public VnPayClient(IConfiguration config)
    {
        _tmnCode         = config["VnPay:TmnCode"]!;
        _hashSecret      = config["VnPay:HashSecret"]!;
        _paymentUrl      = config["VnPay:PaymentUrl"]!;
        _returnUrl       = config["VnPay:ReturnUrl"]!;
        _frontendBaseUrl = config["VnPay:FrontendBaseUrl"] ?? "http://localhost:5173";
        _refundUrl       = config["VnPay:RefundUrl"] ?? "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";
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

    /// <summary>
    /// Gọi VNPay Refund API. Trả về (true, "OK") khi thành công, (false, message) khi thất bại.
    /// vnpayTransactionNo = vnp_TransactionNo từ callback lúc thanh toán.
    /// transactionDate = ngày thanh toán gốc, format yyyyMMddHHmmss UTC+7.
    /// </summary>
    public async Task<(bool Success, string Message)> RefundAsync(
        string txnRef,
        string vnpayTransactionNo,
        decimal amount,
        string orderInfo,
        string transactionDate,
        string createBy,
        string clientIp,
        bool isFullRefund = true)
    {
        var requestId       = Guid.NewGuid().ToString("N")[..20];
        var createDate      = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss");
        var transactionType = isFullRefund ? "02" : "03";
        var amountStr       = ((long)(amount * 100)).ToString();
        var ip              = string.IsNullOrWhiteSpace(clientIp) || clientIp == "::1" ? "127.0.0.1" : clientIp;

        // VNPay refund API hash: pipe-separated, vnp_TmnCode appears at positions 4 and 13
        var hashInput = string.Join("|",
            requestId, "2.1.0", "refund", _tmnCode, transactionType,
            txnRef, amountStr, orderInfo, transactionDate,
            createBy, createDate, ip, _tmnCode);
        var secureHash = HmacSha512(_hashSecret, hashInput);

        var body = new
        {
            vnp_RequestId       = requestId,
            vnp_Version         = "2.1.0",
            vnp_Command         = "refund",
            vnp_TmnCode         = _tmnCode,
            vnp_TransactionType = transactionType,
            vnp_TxnRef          = txnRef,
            vnp_Amount          = amountStr,
            vnp_OrderInfo       = orderInfo,
            vnp_TransactionNo   = vnpayTransactionNo,
            vnp_TransactionDate = transactionDate,
            vnp_CreateBy        = createBy,
            vnp_CreateDate      = createDate,
            vnp_IpAddr          = ip,
            vnp_SecureHash      = secureHash,
        };

        try
        {
            using var response = await _http.PostAsJsonAsync(_refundUrl, body);
            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            if (result is null) return (false, "Empty response from VNPay");
            result.TryGetValue("vnp_ResponseCode", out var code);
            result.TryGetValue("vnp_Message", out var msg);
            return (code == "00", msg ?? "Unknown");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

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
