using HashProcessing.Worker.Core;
using Microsoft.EntityFrameworkCore;

namespace HashProcessing.Worker.Infrastructure;

public class HashRepository(HashDbContext db) : IHashRepository
{
    private readonly HashDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task SaveBatchAsync(IReadOnlyCollection<HashEntity> entities, CancellationToken ct = default)
    {
        if (entities.Count == 0)
            return;

        var parameters = entities.SelectMany(e => new object[] { e.Id, e.Date, e.Sha1 }).ToList();
        var valueClauses = entities.Select((_, i) => $"({{{i * 3}}}, {{{i * 3 + 1}}}, {{{i * 3 + 2}}})");

        var sql = $"INSERT IGNORE INTO hashes (id, date, sha1) VALUES {string.Join(", ", valueClauses)}";
        await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }

    public async Task<IReadOnlyDictionary<DateOnly, long>> GetCountsByDatesAsync(
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken ct = default)
    {
        return await _db.Hashes
            .Where(h => dates.Contains(h.Date))
            .GroupBy(h => h.Date)
            .ToDictionaryAsync(g => g.Key, g => (long)g.Count(), ct);
    }
}
