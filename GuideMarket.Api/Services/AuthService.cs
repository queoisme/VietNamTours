using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Infrastructure;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly SupabaseAuthClient _supabase;
    private readonly IOtpService _otp;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork uow,
        SupabaseAuthClient supabase,
        IOtpService otp,
        ILogger<AuthService> logger)
    {
        _uow      = uow;
        _supabase = supabase;
        _otp      = otp;
        _logger   = logger;
    }

    // ----------------------------------------------------------------
    // Register
    // ----------------------------------------------------------------
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var existing = await _uow.Users.GetByEmailAsync(request.Email);
        if (existing is not null)
            throw new InvalidOperationException("Email đã được sử dụng");

        // Tạo user trong Supabase với email_confirm=true (bypass Supabase email)
        SupabaseUserDto supabaseUser;
        try
        {
            supabaseUser = await _supabase.AdminCreateUserAsync(
                request.Email, request.Password, request.FullName);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(ex.Message);
        }

        // Lưu vào local DB — is_verified = false cho đến khi verify OTP
        var user = new User
        {
            Id         = supabaseUser.Id,
            Email      = request.Email,
            FullName   = request.FullName,
            Role       = UserRole.customer,
            IsVerified = false,
            CreatedAt  = DateTimeOffset.UtcNow,
            UpdatedAt  = DateTimeOffset.UtcNow,
        };
        await _uow.Users.AddAsync(user);
        await _uow.SaveChangesAsync();

        // Gửi OTP — nếu email thất bại, không rollback đăng ký; user dùng resend-verify-email
        try
        {
            await _otp.GenerateAndSendAsync(request.Email, OtpTypes.EmailRegistration);
            _logger.LogInformation("User {Email} registered, OTP sent", request.Email);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "User {Email} registered but OTP email failed — user can resend", request.Email);
        }

        return new RegisterResponse { Email = request.Email };
    }

    // ----------------------------------------------------------------
    // Login
    // ----------------------------------------------------------------
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        // Kiểm tra local DB trước để chặn user chưa verify hoặc bị ban
        var localUser = await _uow.Users.GetByEmailAsync(request.Email);
        if (localUser is not null)
        {
            if (localUser.IsBanned)
                throw new UnauthorizedAccessException("Tài khoản của bạn đã bị khóa");

            if (!localUser.IsVerified)
                throw new UnauthorizedAccessException("Tài khoản chưa được xác thực. Vui lòng kiểm tra email để lấy mã OTP.");
        }

        SupabaseTokenResponse token;
        try
        {
            token = await _supabase.SignInWithPasswordAsync(request.Email, request.Password);
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException("Email hoặc mật khẩu không đúng");
        }

        var user = localUser
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản. Vui lòng đăng ký.");

        return new LoginResponse
        {
            AccessToken  = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresIn    = token.ExpiresIn,
            User = new UserResponse
            {
                Id         = user.Id,
                Email      = user.Email,
                FullName   = user.FullName,
                Phone      = user.Phone,
                AvatarUrl  = user.AvatarUrl,
                Role       = user.Role.ToString(),
                IsVerified = user.IsVerified,
                IsBanned   = user.IsBanned,
                CreatedAt  = user.CreatedAt,
            }
        };
    }

    // ----------------------------------------------------------------
    // Refresh token
    // ----------------------------------------------------------------
    public async Task<LoginResponse> RefreshTokenAsync(string refreshToken)
    {
        SupabaseTokenResponse token;
        try
        {
            token = await _supabase.RefreshTokenAsync(refreshToken);
        }
        catch
        {
            throw new UnauthorizedAccessException("Invalid or expired refresh token");
        }

        var userId = token.User?.Id ?? Guid.Empty;
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");

        return new LoginResponse
        {
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresIn = token.ExpiresIn,
            User = new UserResponse
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.Phone,
                AvatarUrl = user.AvatarUrl,
                Role = user.Role.ToString(),
                IsVerified = user.IsVerified,
                IsBanned = user.IsBanned,
                CreatedAt = user.CreatedAt,
            }
        };
    }

    // ----------------------------------------------------------------
    // Logout
    // ----------------------------------------------------------------
    public async Task LogoutAsync(string accessToken)
    {
        await _supabase.SignOutAsync(accessToken);
    }

    // ----------------------------------------------------------------
    // Forgot password
    // ----------------------------------------------------------------
    public async Task ForgotPasswordAsync(string email)
    {
        // Không reveal email có tồn tại hay không (anti-enumeration)
        var user = await _uow.Users.GetByEmailAsync(email);
        if (user is not null)
            await _otp.GenerateAndSendAsync(email, OtpTypes.PasswordReset);

        _logger.LogInformation("Password reset OTP requested for {Email}", email);
    }

    // ----------------------------------------------------------------
    // Reset password
    // ----------------------------------------------------------------
    public async Task ResetPasswordAsync(ResetPasswordRequest request)
    {
        var (result, otpId) = await _otp.ValidateAsync(
            request.Email, request.Otp, OtpTypes.PasswordReset);

        switch (result)
        {
            case OtpValidationResult.Valid:
                break;
            case OtpValidationResult.Expired:
                throw new InvalidOperationException("Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.");
            case OtpValidationResult.MaxAttemptsExceeded:
                throw new InvalidOperationException("Đã vượt quá số lần thử. Vui lòng yêu cầu mã mới.");
            default:
                throw new InvalidOperationException("Mã OTP không hợp lệ.");
        }

        var user = await _uow.Users.GetByEmailAsync(request.Email)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản");

        await _supabase.AdminUpdatePasswordAsync(user.Id, request.NewPassword);
        await _otp.MarkUsedAsync(otpId);

        _logger.LogInformation("Password reset successful for {Email}", request.Email);
    }

    // ----------------------------------------------------------------
    // Verify email OTP
    // ----------------------------------------------------------------
    public async Task<VerifyEmailResponse> VerifyEmailAsync(string email, string otp)
    {
        var (result, otpId) = await _otp.ValidateAsync(email, otp, OtpTypes.EmailRegistration);

        switch (result)
        {
            case OtpValidationResult.Valid:
                break;
            case OtpValidationResult.NotFound:
                throw new InvalidOperationException("Không tìm thấy mã OTP. Vui lòng yêu cầu mã mới.");
            case OtpValidationResult.Expired:
                throw new InvalidOperationException("Mã OTP đã hết hạn. Vui lòng yêu cầu mã mới.");
            case OtpValidationResult.MaxAttemptsExceeded:
                throw new InvalidOperationException("Đã vượt quá số lần thử. Vui lòng yêu cầu mã mới.");
            default:
                throw new InvalidOperationException("Mã OTP không hợp lệ.");
        }

        var user = await _uow.Users.GetByEmailAsync(email)
            ?? throw new KeyNotFoundException("Không tìm thấy tài khoản");

        if (!user.IsVerified)
        {
            user.IsVerified = true;
            user.UpdatedAt  = DateTimeOffset.UtcNow;
            _uow.Users.Update(user);
            await _uow.SaveChangesAsync();
        }

        await _otp.MarkUsedAsync(otpId);

        _logger.LogInformation("Email {Email} verified successfully", email);

        return new VerifyEmailResponse { Email = email };
    }

    public async Task ResendVerifyEmailAsync(string email)
    {
        var user = await _uow.Users.GetByEmailAsync(email);
        if (user is null || user.IsVerified)
            return; // Không reveal trạng thái

        await _otp.GenerateAndSendAsync(email, OtpTypes.EmailRegistration);
        _logger.LogInformation("Resent OTP to {Email}", email);
    }

    // ----------------------------------------------------------------
    // Phone OTP
    // ----------------------------------------------------------------
    public async Task RequestOtpAsync(string phone)
    {
        await _supabase.SendPhoneOtpAsync(phone);
        _logger.LogInformation("OTP sent to {Phone}", phone);
    }

    public async Task VerifyPhoneAsync(Guid userId, string phone, string token)
    {
        await _supabase.VerifyPhoneOtpAsync(phone, token);

        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");
        user.Phone = phone;
        _uow.Users.Update(user);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Phone {Phone} verified for user {UserId}", phone, userId);
    }

    // ----------------------------------------------------------------
    // Webhook (email/password sign-up và OAuth sign-up)
    // ----------------------------------------------------------------
    public async Task CreateUserFromWebhookAsync(Guid userId, string email, string fullName, string? avatarUrl = null)
    {
        var exists = await _uow.Users.ExistsAsync(userId);
        if (exists) return;

        await _uow.Users.AddAsync(new User
        {
            Id        = userId,
            Email     = email,
            FullName  = fullName,
            AvatarUrl = avatarUrl,
            Role      = UserRole.customer,
            IsVerified = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _uow.SaveChangesAsync();
        _logger.LogInformation("User {UserId} created from webhook", userId);
    }

    // ----------------------------------------------------------------
    // Google OAuth
    // ----------------------------------------------------------------
    public string GetGoogleOAuthUrl(string? redirectTo)
    {
        return _supabase.GetOAuthUrl("google", redirectTo);
    }

    public async Task<LoginResponse> HandleSocialLoginAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId);

        if (user is null)
        {
            // Webhook chưa kịp fire — lấy metadata từ Supabase và tự tạo user
            var supabaseUser = await _supabase.AdminGetUserAsync(userId);
            var fullName  = ExtractFullName(supabaseUser) ?? supabaseUser.Email;
            var avatarUrl = ExtractAvatarUrl(supabaseUser);

            await _uow.Users.AddAsync(new User
            {
                Id        = userId,
                Email     = supabaseUser.Email,
                FullName  = fullName,
                AvatarUrl = avatarUrl,
                Role      = UserRole.customer,
                IsVerified = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            });
            await _uow.SaveChangesAsync();

            user = await _uow.Users.GetByIdAsync(userId)!;
            _logger.LogInformation("User {UserId} created via social login fallback", userId);
        }

        if (user!.IsBanned)
            throw new UnauthorizedAccessException("Tài khoản của bạn đã bị khóa");

        return new LoginResponse
        {
            AccessToken  = string.Empty,   // frontend đã có token
            RefreshToken = string.Empty,
            ExpiresIn    = 0,
            User = new UserResponse
            {
                Id         = user.Id,
                Email      = user.Email,
                FullName   = user.FullName,
                Phone      = user.Phone,
                AvatarUrl  = user.AvatarUrl,
                Role       = user.Role.ToString(),
                IsVerified = user.IsVerified,
                IsBanned   = user.IsBanned,
                CreatedAt  = user.CreatedAt,
            }
        };
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------
    private static string? ExtractFullName(SupabaseUserDto user)
    {
        if (user.UserMetadata?.TryGetValue("full_name", out var fn) == true
            && fn.ValueKind == System.Text.Json.JsonValueKind.String)
            return fn.GetString();

        if (user.RawUserMetaData?.TryGetValue("full_name", out var rfn) == true
            && rfn.ValueKind == System.Text.Json.JsonValueKind.String)
            return rfn.GetString();

        if (user.RawUserMetaData?.TryGetValue("name", out var n) == true
            && n.ValueKind == System.Text.Json.JsonValueKind.String)
            return n.GetString();

        return null;
    }

    private static string? ExtractAvatarUrl(SupabaseUserDto user)
    {
        if (user.RawUserMetaData?.TryGetValue("avatar_url", out var av) == true
            && av.ValueKind == System.Text.Json.JsonValueKind.String)
            return av.GetString();

        if (user.RawUserMetaData?.TryGetValue("picture", out var pic) == true
            && pic.ValueKind == System.Text.Json.JsonValueKind.String)
            return pic.GetString();

        return null;
    }
}
