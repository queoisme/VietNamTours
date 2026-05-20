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
    public string Message { get; set; } = "Đăng ký thành công. Vui lòng kiểm tra email để lấy mã xác thực.";
}

public class VerifyEmailResponse
{
    public string Email { get; set; } = default!;
    public string Message { get; set; } = "Xác thực email thành công. Vui lòng đăng nhập để tiếp tục.";
}
