using HashProcessing.Worker.Core;

namespace HashProcessing.Benchmarks.Infrastructure;

public sealed class CountingHashRepository(CompletionSignal signal) : IHashRepository
{
    public Task SaveBatchAsync(IReadOnlyCollection<HashEntity> entities, CancellationToken ct = default)
    {
        signal.Decrement();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyDictionary<DateOnly, long>> GetCountsByDatesAsync(
        IReadOnlyCollection<DateOnly> dates,
        CancellationToken ct = default)
    {
        IReadOnlyDictionary<DateOnly, long> result = dates.ToDictionary(d => d, _ => 0L);
        return Task.FromResult(result);
    }
}
