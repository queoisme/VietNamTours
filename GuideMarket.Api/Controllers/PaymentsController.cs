using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Infrastructure;
using GuideMarket.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GuideMarket.Api.Controllers;

[ApiController]
[Route("api/v1/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IBookingService      _bookingService;
    private readonly IBoostService        _boostService;
    private readonly ISubscriptionService _subscriptionService;
    private readonly VnPayClient          _vnPay;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IBookingService bookingService,
        IBoostService boostService,
        ISubscriptionService subscriptionService,
        VnPayClient vnPay,
        ILogger<PaymentsController> logger)
    {
        _bookingService      = bookingService;
        _boostService        = boostService;
        _subscriptionService = subscriptionService;
        _vnPay               = vnPay;
        _logger              = logger;
    }

    [HttpPost("vnpay/create")]
    [Authorize]
    public async Task<IActionResult> CreatePaymentUrl([FromBody] CreateVnPayRequest request)
    {
        var userId  = GetCurrentUserId();
        var booking = await _bookingService.GetByIdAsync(userId, request.BookingId);

        if (booking.CustomerId != userId)
            return StatusCode(403, ApiResponse<object>.Fail("Not your booking"));
        if (booking.Status != "pending" || booking.PaymentStatus != "unpaid")
            return UnprocessableEntity(ApiResponse<object>.Fail("Booking is not in a payable state"));

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var orderInfo = $"Thanh toan booking {request.BookingId}";
        var url       = _vnPay.CreatePaymentUrl(booking.PaymentTxnId!, booking.TotalPrice, orderInfo, ipAddress);

        return Ok(ApiResponse<VnPayPaymentUrlResponse>.Ok(new VnPayPaymentUrlResponse { PaymentUrl = url }));
    }

    /// <summary>VNPay IPN callback. Always returns 200 with RspCode per VNPay spec.</summary>
    [HttpGet("vnpay/ipn")]
    public async Task<IActionResult> Ipn()
    {
        var (isValid, txnRef, responseCode) = _vnPay.VerifyIpn(Request.Query);

        if (!isValid)
        {
            _logger.LogWarning("VNPay IPN: invalid signature for txnRef {TxnRef}", txnRef);
            return Ok(new { RspCode = "97", Message = "Invalid signature" });
        }

        if (responseCode == "00")
        {
            try
            {
                if (txnRef.StartsWith("bt"))
                    await _boostService.HandlePaymentSuccessAsync(txnRef);
                else if (txnRef.StartsWith("sb"))
                    await _subscriptionService.HandlePaymentSuccessAsync(txnRef);
                else
                    await _bookingService.HandlePaymentSuccessAsync(txnRef);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VNPay IPN: error processing txnRef {TxnRef}", txnRef);
                return Ok(new { RspCode = "99", Message = "Internal error" });
            }
        }
        else
        {
            _logger.LogInformation(
                "VNPay IPN: payment failed for txnRef {TxnRef}, responseCode {Code}",
                txnRef, responseCode);
        }

        return Ok(new { RspCode = "00", Message = "Confirm success" });
    }

    /// <summary>VNPay return redirect — browser redirect after payment.</summary>
    [HttpGet("vnpay/return")]
    public IActionResult Return()
    {
        var responseCode = Request.Query["vnp_ResponseCode"].ToString();
        var txnRef       = Request.Query["vnp_TxnRef"].ToString();

        var redirectUrl = responseCode == "00"
            ? _vnPay.GetFrontendSuccessUrl(txnRef)
            : _vnPay.GetFrontendFailedUrl(responseCode);

        return Redirect(redirectUrl);
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(sub, out var userId))
            throw new UnauthorizedAccessException("Invalid token subject");
        return userId;
    }
}
