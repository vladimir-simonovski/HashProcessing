namespace HashProcessing.Worker.Core;

public interface IHashRepository
{
    Task SaveBatchAsync(IReadOnlyCollection<HashEntity> entities, CancellationToken ct = default);
    Task<IReadOnlyDictionary<DateOnly, long>> GetCountsByDatesAsync(IReadOnlyCollection<DateOnly> dates, CancellationToken ct = default);
}
