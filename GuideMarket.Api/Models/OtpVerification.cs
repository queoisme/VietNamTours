using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

public enum OtpType
{
    email_registration,
    password_reset,
    phone_verification,
}

[Table("otp_verifications")]
public class OtpVerification
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("target")]
    [MaxLength(255)]
    public string Target { get; set; } = default!;

    [Column("type")]
    public string Type { get; set; } = default!;

    [Column("code")]
    [MaxLength(6)]
    public string Code { get; set; } = default!;

    [Column("expires_at")]
    public DateTimeOffset ExpiresAt { get; set; }

    [Column("is_used")]
    public bool IsUsed { get; set; } = false;

    [Column("attempts")]
    public int Attempts { get; set; } = 0;

    [Column("ip_address")]
    [MaxLength(50)]
    public string? IpAddress { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
