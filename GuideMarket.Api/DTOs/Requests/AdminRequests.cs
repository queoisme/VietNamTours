namespace GuideMarket.Api.DTOs.Requests;

public class BanUserRequest
{
    public bool IsBanned { get; set; }
    public string? Reason { get; set; }
}
