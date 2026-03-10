namespace HashProcessing.Worker.Core;

public interface IHashRepository
{
    Task SaveBatchAsync(IReadOnlyCollection<HashEntity> entities, CancellationToken ct = default);
    Task<long> GetCountByDateAsync(DateOnly date, CancellationToken ct = default);
}
