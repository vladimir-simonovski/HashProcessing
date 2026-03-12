using HashProcessing.Api.Core;
using Microsoft.EntityFrameworkCore;

namespace HashProcessing.Api.Infrastructure;

public class HashDailyCountRepository(ApiDbContext db) : IHashDailyCountRepository
{
    private readonly ApiDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task UpsertAsync(DateOnly date, long count, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO hash_daily_counts (date, count) VALUES ({date}, {count}) ON DUPLICATE KEY UPDATE count = GREATEST(count, {count})",
            ct);
    }

    public async Task<IReadOnlyCollection<HashDailyCount>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.HashDailyCounts
            .OrderByDescending(h => h.Date)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
