namespace GuideMarket.Api.DTOs.Responses;

public class GuideApplicationResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Location { get; set; }
    public string Bio { get; set; } = default!;
    public short ExperienceYears { get; set; }
    public string[] Languages { get; set; } = [];
    public List<CertificationItem> Certifications { get; set; } = [];
    public string IdentityDocUrl { get; set; } = default!;
    public string[] CertificateUrls { get; set; } = [];
    public string Status { get; set; } = default!;
    public string? RejectionReason { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Applicant info (for admin view)
    public string? ApplicantEmail { get; set; }
    public string? ApplicantAvatarUrl { get; set; }
}
