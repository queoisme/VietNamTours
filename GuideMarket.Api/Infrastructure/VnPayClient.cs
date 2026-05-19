using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

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
        _frontendBaseUrl = config["VnPay:FrontendBaseUrl"] ?? "http://localhost:3000";
    }

    public string CreatePaymentUrl(string txnRef, decimal amount, string orderInfo, string ipAddress)
    {
        // VNPay amount = total_price * 100 (no decimals)
        var vnpAmount  = ((long)(amount * 100)).ToString();
        var createDate = DateTime.UtcNow.AddHours(7).ToString("yyyyMMddHHmmss"); // ICT (UTC+7)
        var expireDate = DateTime.UtcNow.AddHours(7).AddMinutes(15).ToString("yyyyMMddHHmmss");

        var @params = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Version"]    = "2.1.0",
            ["vnp_Command"]    = "pay",
            ["vnp_TmnCode"]    = _tmnCode,
            ["vnp_Amount"]     = vnpAmount,
            ["vnp_CurrCode"]   = "VND",
            ["vnp_TxnRef"]     = txnRef,
            ["vnp_OrderInfo"]  = orderInfo,
            ["vnp_OrderType"]  = "other",
            ["vnp_Locale"]     = "vn",
            ["vnp_ReturnUrl"]  = _returnUrl,
            ["vnp_IpAddr"]     = ipAddress,
            ["vnp_CreateDate"] = createDate,
            ["vnp_ExpireDate"] = expireDate,
        };

        var signData   = BuildSignData(@params);
        var secureHash = HmacSha512(_hashSecret, signData);

        return $"{_paymentUrl}?{signData}&vnp_SecureHash={secureHash}";
    }

    public (bool IsValid, string TxnRef, string ResponseCode) VerifyIpn(IQueryCollection query)
    {
        var receivedHash = query["vnp_SecureHash"].ToString();
        var txnRef       = query["vnp_TxnRef"].ToString();
        var responseCode = query["vnp_ResponseCode"].ToString();

        var @params = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in query)
        {
            if (key != "vnp_SecureHash" && key != "vnp_SecureHashType")
                @params[key] = value.ToString();
        }

        var signData     = BuildSignData(@params);
        var expectedHash = HmacSha512(_hashSecret, signData);
        var isValid      = string.Equals(expectedHash, receivedHash, StringComparison.OrdinalIgnoreCase);

        return (isValid, txnRef, responseCode);
    }

    public string GetFrontendSuccessUrl(string txnRef) =>
        $"{_frontendBaseUrl}/payment/success?txnRef={txnRef}";

    public string GetFrontendFailedUrl(string responseCode) =>
        $"{_frontendBaseUrl}/payment/failed?code={responseCode}";

    private static string BuildSignData(SortedDictionary<string, string> @params) =>
        string.Join("&", @params.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

    private static string HmacSha512(string key, string data)
    {
        var keyBytes  = Encoding.UTF8.GetBytes(key);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        using var hmac = new HMACSHA512(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(dataBytes)).ToLower();
    }
}
