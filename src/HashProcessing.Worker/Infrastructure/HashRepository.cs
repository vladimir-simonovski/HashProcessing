using HashProcessing.Worker.Core;

namespace HashProcessing.Worker.Infrastructure;

public class HashRepository(HashDbContext db) : IHashRepository
{
    private readonly HashDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task SaveBatchAsync(IReadOnlyCollection<HashEntity> entities, CancellationToken ct = default)
    {
        _db.Hashes.AddRange(entities);
        await _db.SaveChangesAsync(ct);
    }
}
