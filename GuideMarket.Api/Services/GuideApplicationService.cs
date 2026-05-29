using System.Text.Json;
using GuideMarket.Api.DTOs.Requests;
using GuideMarket.Api.DTOs.Responses;
using GuideMarket.Api.Exceptions;
using GuideMarket.Api.Infrastructure;
using GuideMarket.Api.Models;
using GuideMarket.Api.Repositories;
using GuideMarket.Api.Services.Interfaces;

namespace GuideMarket.Api.Services;

public class GuideApplicationService : IGuideApplicationService
{
    private readonly IUnitOfWork _uow;
    private readonly SupabaseAuthClient _supabase;
    private readonly INotificationService _notifications;
    private readonly SupabaseStorageClient _storage;

    public GuideApplicationService(
        IUnitOfWork uow,
        SupabaseAuthClient supabase,
        INotificationService notifications,
        SupabaseStorageClient storage)
    {
        _uow           = uow;
        _supabase      = supabase;
        _notifications = notifications;
        _storage       = storage;
    }

    public async Task<GuideApplicationResponse> SubmitAsync(Guid userId, CreateGuideApplicationRequest request)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");

        if (user.Role == UserRole.guide)
            throw new InvalidOperationException("You are already a guide");

        if (await _uow.GuideApplications.HasPendingOrApprovedAsync(userId))
            throw new InvalidOperationException("You already have a pending or approved application");

        var application = new GuideApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            FullName = request.FullName,
            Phone = request.Phone,
            Location = request.Location,
            Bio = request.Bio,
            ExperienceYears = request.ExperienceYears,
            Languages = request.Languages,
            Certifications = JsonSerializer.Serialize(request.Certifications),
            IdentityDocUrl = request.IdentityDocUrl,
            CertificateUrls = request.CertificateUrls,
            Status = ApplicationStatus.pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _uow.GuideApplications.AddAsync(application);
        await _uow.SaveChangesAsync();

        return MapToResponse(application, user);
    }

    public async Task<GuideApplicationResponse?> GetMyLatestApplicationAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");
        var apps = await _uow.GuideApplications.GetByUserIdAsync(userId);
        // Already ordered DESC by CreatedAt — return latest or null
        return apps.Count == 0 ? null : MapToResponse(apps[0], user);
    }

    public async Task<(List<GuideApplicationResponse> Items, long Total)> GetAllAsync(Guid adminId, GuideApplicationListParams p)
    {
        await RequireAdminAsync(adminId);
        var (apps, total) = await _uow.GuideApplications.GetAllAsync(p);
        return (apps.Select(a => MapToResponse(a, a.Applicant)).ToList(), total);
    }

    public async Task<GuideApplicationResponse?> GetByIdAsync(Guid adminId, Guid applicationId)
    {
        await RequireAdminAsync(adminId);
        var app = await _uow.GuideApplications.GetByIdWithUsersAsync(applicationId);
        if (app is null) return null;

        var response = MapToResponse(app, app.Applicant);

        // Generate signed URL for private identity document (TTL 15 min)
        if (!string.IsNullOrWhiteSpace(app.IdentityDocUrl)
            && !app.IdentityDocUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var slashIdx = app.IdentityDocUrl.IndexOf('/');
                if (slashIdx > 0)
                {
                    var bucket = app.IdentityDocUrl[..slashIdx];
                    var path   = app.IdentityDocUrl[(slashIdx + 1)..];
                    response.IdentityDocUrl = await _storage.CreateSignedUrlAsync(bucket, path);
                }
            }
            catch
            {
                // Keep original path if signing fails — admin can investigate manually
            }
        }

        return response;
    }

    public async Task<GuideApplicationResponse> ApproveAsync(Guid adminId, Guid applicationId)
    {
        await RequireAdminAsync(adminId);

        var app = await _uow.GuideApplications.GetByIdWithUsersAsync(applicationId)
            ?? throw new KeyNotFoundException("Application not found");

        if (app.Status != ApplicationStatus.pending)
            throw new InvalidOperationException($"Cannot approve an application with status '{app.Status}'");

        // 1. Create guide_profile
        var profileExists = await _uow.GuideProfiles.ExistsByUserIdAsync(app.UserId);
        if (!profileExists)
        {
            var profile = new GuideProfile
            {
                Id = Guid.NewGuid(),
                UserId = app.UserId,
                Bio = app.Bio,
                ExperienceYears = app.ExperienceYears,
                Languages = app.Languages,
                Certifications = app.Certifications,
                IdentityDocUrl = app.IdentityDocUrl,
                VerificationStatus = VerificationStatus.approved,
                SubscriptionPlan = SubscriptionPlan.free,
            };
            await _uow.GuideProfiles.AddAsync(profile);
        }

        // 2. Update user role
        var user = app.Applicant;
        user.Role = UserRole.guide;
        user.IsVerified = true;
        _uow.Users.Update(user);

        // 3. Mark application approved
        app.Status = ApplicationStatus.approved;
        app.ReviewedBy = adminId;
        app.ReviewedAt = DateTimeOffset.UtcNow;
        _uow.GuideApplications.Update(app);

        await _uow.SaveChangesAsync();

        // 4. Sync role to Supabase Auth app_metadata (so new JWTs carry the updated role)
        await _supabase.AdminUpdateUserRoleAsync(app.UserId, "guide");

        await _notifications.CreateAsync(
            app.UserId, "profile_approved", "Hồ sơ guide của bạn đã được duyệt!",
            "Chúc mừng! Bạn đã trở thành guide trên VietNamTours.",
            "guide_application", app.Id,
            "Hồ sơ guide được duyệt - VietNamTours",
            "<p>Chúc mừng! Hồ sơ guide của bạn đã được duyệt. Bạn có thể bắt đầu đăng tour ngay.</p>");

        return MapToResponse(app, user);
    }

    public async Task<GuideApplicationResponse> RejectAsync(Guid adminId, Guid applicationId, RejectApplicationRequest request)
    {
        await RequireAdminAsync(adminId);

        var app = await _uow.GuideApplications.GetByIdWithUsersAsync(applicationId)
            ?? throw new KeyNotFoundException("Application not found");

        if (app.Status != ApplicationStatus.pending)
            throw new InvalidOperationException($"Cannot reject an application with status '{app.Status}'");

        app.Status = ApplicationStatus.rejected;
        app.RejectionReason = request.RejectionReason;
        app.ReviewedBy = adminId;
        app.ReviewedAt = DateTimeOffset.UtcNow;
        _uow.GuideApplications.Update(app);

        await _uow.SaveChangesAsync();

        await _notifications.CreateAsync(
            app.UserId, "profile_rejected", "Hồ sơ guide bị từ chối",
            $"Hồ sơ guide của bạn đã bị từ chối. Lý do: {request.RejectionReason}",
            "guide_application", app.Id,
            "Hồ sơ guide bị từ chối - VietNamTours",
            $"<p>Hồ sơ guide của bạn đã bị từ chối.</p><p>Lý do: {request.RejectionReason}</p>");

        return MapToResponse(app, app.Applicant);
    }

    private async Task RequireAdminAsync(Guid userId)
    {
        var user = await _uow.Users.GetByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found");
        if (user.Role != UserRole.admin)
            throw new ForbiddenAccessException("Only admins can perform this action");
    }

    private static GuideApplicationResponse MapToResponse(GuideApplication a, User applicant) => new()
    {
        Id = a.Id,
        UserId = a.UserId,
        FullName = a.FullName,
        Phone = a.Phone,
        Location = a.Location,
        Bio = a.Bio,
        ExperienceYears = a.ExperienceYears,
        Languages = a.Languages,
        Certifications = DeserializeCerts(a.Certifications),
        IdentityDocUrl = a.IdentityDocUrl,
        CertificateUrls = a.CertificateUrls,
        Status = a.Status.ToString(),
        RejectionReason = a.RejectionReason,
        ReviewedBy = a.ReviewedBy,
        ReviewedAt = a.ReviewedAt,
        CreatedAt = a.CreatedAt,
        ApplicantEmail = applicant.Email,
        ApplicantAvatarUrl = applicant.AvatarUrl,
    };

    private static List<CertificationItem> DeserializeCerts(string json)
    {
        try { return JsonSerializer.Deserialize<List<CertificationItem>>(json) ?? []; }
        catch { return []; }
    }
}
