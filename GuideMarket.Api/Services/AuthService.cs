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
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork uow,
        SupabaseAuthClient supabase,
        ILogger<AuthService> logger)
    {
        _uow = uow;
        _supabase = supabase;
        _logger = logger;
    }

    // ----------------------------------------------------------------
    // Register
    // ----------------------------------------------------------------
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        try
        {
            await _supabase.SignUpAsync(request.Email, request.Password, request.FullName);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(ex.Message);
        }

        _logger.LogInformation("User {Email} signed up, awaiting email confirmation", request.Email);

        return new RegisterResponse { Email = request.Email };
    }

    // ----------------------------------------------------------------
    // Login
    // ----------------------------------------------------------------
    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        SupabaseTokenResponse token;
        try
        {
            token = await _supabase.SignInWithPasswordAsync(request.Email, request.Password);
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException("Invalid email or password");
        }

        // Lấy thông tin user từ DB; lazy-create nếu webhook chưa kịp chạy
        var userId = token.User?.Id ?? Guid.Empty;
        var user = userId != Guid.Empty
            ? await _uow.Users.GetByIdAsync(userId)
            : await _uow.Users.GetByEmailAsync(request.Email);

        if (user is null)
        {
            // Email đã được Supabase xác nhận (login thành công) → tạo user trong DB
            var fullName = token.User?.UserMetadata != null &&
                           token.User.UserMetadata.TryGetValue("full_name", out var fn)
                ? fn.GetString() ?? string.Empty
                : string.Empty;

            user = new User
            {
                Id = userId != Guid.Empty ? userId : Guid.NewGuid(),
                Email = request.Email,
                FullName = fullName,
                Role = UserRole.customer,
                IsVerified = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            await _uow.Users.AddAsync(user);
            await _uow.SaveChangesAsync();
        }

        if (user.IsBanned)
            throw new UnauthorizedAccessException("Your account has been banned");

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
        await _supabase.SendPasswordRecoveryAsync(email);
        _logger.LogInformation("Password recovery email sent to {Email}", email);
    }

    // ----------------------------------------------------------------
    // Reset password
    // ----------------------------------------------------------------
    public async Task ResetPasswordAsync(string accessToken, string newPassword)
    {
        await _supabase.UpdatePasswordAsync(accessToken, newPassword);
    }

    // ----------------------------------------------------------------
    // Verify email OTP
    // ----------------------------------------------------------------
    public async Task<LoginResponse> VerifyEmailAsync(string email, string otp)
    {
        SupabaseTokenResponse token;
        try
        {
            token = await _supabase.VerifyEmailOtpAsync(email, otp);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(ex.Message);
        }

        var userId = token.User?.Id ?? Guid.Empty;
        var user = userId != Guid.Empty
            ? await _uow.Users.GetByIdAsync(userId)
            : await _uow.Users.GetByEmailAsync(email);

        if (user is null)
        {
            var fullName = token.User?.UserMetadata != null &&
                           token.User.UserMetadata.TryGetValue("full_name", out var fn)
                ? fn.GetString() ?? string.Empty
                : string.Empty;

            user = new User
            {
                Id = userId != Guid.Empty ? userId : Guid.NewGuid(),
                Email = email,
                FullName = fullName,
                Role = UserRole.customer,
                IsVerified = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            await _uow.Users.AddAsync(user);
            await _uow.SaveChangesAsync();
        }
        else if (!user.IsVerified)
        {
            user.IsVerified = true;
            _uow.Users.Update(user);
            await _uow.SaveChangesAsync();
        }

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
    // Webhook fallback (giữ lại)
    // ----------------------------------------------------------------
    public async Task CreateUserFromWebhookAsync(Guid userId, string email, string fullName)
    {
        var exists = await _uow.Users.ExistsAsync(userId);
        if (exists) return;

        await _uow.Users.AddAsync(new User
        {
            Id = userId,
            Email = email,
            FullName = fullName,
            Role = UserRole.customer,
            IsVerified = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        });
        await _uow.SaveChangesAsync();
        _logger.LogInformation("User {UserId} created from webhook", userId);
    }
}
