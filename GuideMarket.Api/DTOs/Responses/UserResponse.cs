namespace GuideMarket.Api.DTOs.Responses;

public class UserResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = default!;
    public bool IsVerified { get; set; }
    public bool IsBanned { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
