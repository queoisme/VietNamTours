namespace GuideMarket.Api.DTOs.Responses;

public class LoginResponse
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public int ExpiresIn { get; set; }
    public UserResponse User { get; set; } = default!;
}

public class RegisterResponse
{
    public string Email { get; set; } = default!;
    public bool OtpSent { get; set; } = true;
    public string Message { get; set; } = "Register successful. Please check your email for the OTP.";
}

public class VerifyEmailResponse
{
    public string Email { get; set; } = default!;
    public string Message { get; set; } = "Email verified successfully. Please sign in to continue.";
}

public class GoogleOAuthUrlResponse
{
    public string Url { get; set; } = default!;
    public string Provider { get; set; } = "google";
}
