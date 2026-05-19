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
    public string Message { get; set; } = "Registration successful. Please check your email to confirm your account.";
}
