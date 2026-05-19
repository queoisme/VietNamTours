using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GuideMarket.Api.Models;

public enum WithdrawalMethod { bank, momo, zalopay, vnpay }
public enum WithdrawalStatus { pending, approved, rejected, completed }

[Table("withdrawals")]
public class Withdrawal
{
    [Key][Column("id")] public Guid Id { get; set; }
    [Column("guide_id")] public Guid GuideId { get; set; }
    [Column("amount")] public decimal Amount { get; set; }
    [Column("fee")] public decimal Fee { get; set; }
    [Column("net_amount")] public decimal NetAmount { get; set; }
    [Column("method")] public WithdrawalMethod Method { get; set; }
    [Column("account_info", TypeName = "jsonb")] public string AccountInfo { get; set; } = "{}";
    [Column("note")] public string? Note { get; set; }
    [Column("status")] public WithdrawalStatus Status { get; set; } = WithdrawalStatus.pending;
    [Column("admin_note")] public string? AdminNote { get; set; }
    [Column("created_at")] public DateTimeOffset CreatedAt { get; set; }
    [Column("processed_at")] public DateTimeOffset? ProcessedAt { get; set; }

    public User Guide { get; set; } = default!;
}
