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
    private readonly MomoClient           _momo;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IBookingService bookingService,
        IBoostService boostService,
        ISubscriptionService subscriptionService,
        MomoClient momo,
        ILogger<PaymentsController> logger)
    {
        _bookingService      = bookingService;
        _boostService        = boostService;
        _subscriptionService = subscriptionService;
        _momo                = momo;
        _logger              = logger;
    }

    [HttpPost("momo/create")]
    [Authorize]
    public async Task<IActionResult> CreatePaymentUrl([FromBody] CreateMomoPaymentRequest request)
    {
        var userId  = GetCurrentUserId();
        var booking = await _bookingService.GetByIdAsync(userId, request.BookingId);

        if (booking.CustomerId != userId)
            return StatusCode(403, ApiResponse<object>.Fail("Not your booking"));
        if (booking.Status != "pending" || booking.PaymentStatus != "unpaid")
            return UnprocessableEntity(ApiResponse<object>.Fail("Booking is not in a payable state"));

        var orderInfo           = $"Thanh toan booking {request.BookingId}";
        var (payUrl, qrCodeUrl) = await _momo.CreatePaymentAsync(booking.PaymentTxnId!, booking.TotalPrice, orderInfo);

        return Ok(ApiResponse<MomoPaymentResponse>.Ok(new MomoPaymentResponse
        {
            PayUrl    = payUrl,
            QrCodeUrl = qrCodeUrl,
        }));
    }

    /// <summary>MoMo IPN callback. Always returns 200 per MoMo spec.</summary>
    [HttpPost("momo/ipn")]
    public async Task<IActionResult> Ipn([FromBody] MomoIpnPayload payload)
    {
        if (!_momo.VerifyIpn(payload))
        {
            _logger.LogWarning("MoMo IPN: invalid signature for orderId {OrderId}", payload.OrderId);
            return Ok(new { resultCode = 97, message = "Invalid signature" });
        }

        if (payload.ResultCode == 0)
        {
            try
            {
                if (payload.OrderId.StartsWith("bt"))
                    await _boostService.HandlePaymentSuccessAsync(payload.OrderId);
                else if (payload.OrderId.StartsWith("sb"))
                    await _subscriptionService.HandlePaymentSuccessAsync(payload.OrderId);
                else
                    await _bookingService.HandlePaymentSuccessAsync(payload.OrderId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MoMo IPN: error processing orderId {OrderId}", payload.OrderId);
                return Ok(new { resultCode = 99, message = "Internal error" });
            }
        }
        else
        {
            _logger.LogInformation(
                "MoMo IPN: payment failed for orderId {OrderId}, resultCode {Code}",
                payload.OrderId, payload.ResultCode);
        }

        return Ok(new { resultCode = 0, message = "Confirm success" });
    }

    /// <summary>MoMo return redirect — browser redirect after payment.</summary>
    [HttpGet("momo/return")]
    public IActionResult Return()
    {
        var resultCode = Request.Query["resultCode"].ToString();
        var orderId    = Request.Query["orderId"].ToString();

        var redirectUrl = resultCode == "0"
            ? _momo.GetFrontendSuccessUrl(orderId)
            : _momo.GetFrontendFailedUrl(resultCode);

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
