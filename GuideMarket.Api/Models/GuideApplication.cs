using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

public enum ApplicationStatus { pending, approved, rejected }

[Table("guide_applications")]
public class GuideApplication
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("full_name")]
    public string FullName { get; set; } = default!;

    [Column("phone")]
    public string Phone { get; set; } = default!;

    [Column("location")]
    public string? Location { get; set; }

    [Column("bio")]
    public string Bio { get; set; } = default!;

    [Column("experience_years")]
    public short ExperienceYears { get; set; }

    [Column("languages")]
    public string[] Languages { get; set; } = [];

    [Column("certifications", TypeName = "jsonb")]
    public string Certifications { get; set; } = "[]";

    [Column("identity_doc_url")]
    public string IdentityDocUrl { get; set; } = default!;

    [Column("certificate_urls")]
    public string[] CertificateUrls { get; set; } = [];

    [Column("status")]
    public ApplicationStatus Status { get; set; } = ApplicationStatus.pending;

    [Column("rejection_reason")]
    public string? RejectionReason { get; set; }

    [Column("reviewed_by")]
    public Guid? ReviewedBy { get; set; }

    [Column("reviewed_at")]
    public DateTimeOffset? ReviewedAt { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    public User Applicant { get; set; } = default!;
    public User? Reviewer { get; set; }
}
