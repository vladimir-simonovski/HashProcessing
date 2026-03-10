using HashProcessing.Worker.Core;
using Microsoft.EntityFrameworkCore;

namespace HashProcessing.Worker.Infrastructure;

public class HashRepository(HashDbContext db) : IHashRepository
{
    private readonly HashDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task SaveBatchAsync(IReadOnlyCollection<HashEntity> entities, CancellationToken ct = default)
    {
        _db.Hashes.AddRange(entities);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<long> GetCountByDateAsync(DateOnly date, CancellationToken ct = default) 
        => await _db.Hashes.CountAsync(h => h.Date == date, ct);
}
