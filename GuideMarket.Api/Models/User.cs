using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

public enum UserRole { customer, guide, admin }

[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("email")]
    public string Email { get; set; } = default!;

    [Column("full_name")]
    public string FullName { get; set; } = default!;

    [Column("phone")]
    public string? Phone { get; set; }

    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [Column("role")]
    public UserRole Role { get; set; } = UserRole.customer;

    [Column("is_verified")]
    public bool IsVerified { get; set; }

    [Column("is_banned")]
    public bool IsBanned { get; set; }

    [Column("ban_reason")]
    public string? BanReason { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }

    [Column("deleted_at")]
    public DateTimeOffset? DeletedAt { get; set; }
}
