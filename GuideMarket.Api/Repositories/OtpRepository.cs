using GuideMarket.Api.Data;
using GuideMarket.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GuideMarket.Api.Repositories;

public class OtpRepository : IOtpRepository
{
    private readonly AppDbContext _db;

    public OtpRepository(AppDbContext db) => _db = db;

    public Task<OtpVerification?> GetActiveAsync(string target, string type) =>
        _db.OtpVerifications
           .Where(o => o.Target == target && o.Type == type && !o.IsUsed && o.ExpiresAt > DateTimeOffset.UtcNow)
           .OrderByDescending(o => o.CreatedAt)
           .FirstOrDefaultAsync();

    public async Task CreateAsync(OtpVerification otp)
    {
        // Vô hiệu hoá OTP cũ chưa dùng của cùng target+type
        var old = await _db.OtpVerifications
            .Where(o => o.Target == otp.Target && o.Type == otp.Type && !o.IsUsed)
            .ToListAsync();

        if (old.Count > 0)
        {
            foreach (var o in old) o.IsUsed = true;
            _db.OtpVerifications.UpdateRange(old);
        }

        await _db.OtpVerifications.AddAsync(otp);
        await _db.SaveChangesAsync();
    }

    public async Task MarkUsedAsync(Guid id)
    {
        await _db.OtpVerifications
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsUsed, true));
    }

    public async Task IncrementAttemptsAsync(Guid id)
    {
        await _db.OtpVerifications
            .Where(o => o.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Attempts, o => o.Attempts + 1));
    }

    public async Task DeleteExpiredAsync(DateTimeOffset before)
    {
        await _db.OtpVerifications
            .Where(o => o.ExpiresAt < before)
            .ExecuteDeleteAsync();
    }
}
