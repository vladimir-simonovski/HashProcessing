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

        var parameters = new List<object>();
        var valueClauses = new List<string>();
        var index = 0;

        foreach (var entity in entities)
        {
            valueClauses.Add($"({{{index}}}, {{{index + 1}}}, {{{index + 2}}})");
            parameters.Add(entity.Id);
            parameters.Add(entity.Date);
            parameters.Add(entity.Sha1);
            index += 3;
        }

        var sql = $"INSERT IGNORE INTO hashes (id, date, sha1) VALUES {string.Join(", ", valueClauses)}";
        await _db.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }

    public async Task<long> GetCountByDateAsync(DateOnly date, CancellationToken ct = default) 
        => await _db.Hashes.CountAsync(h => h.Date == date, ct);
}
