using HashProcessing.Worker.Core;

namespace HashProcessing.Benchmarks.Infrastructure;

public sealed class CountingHashRepository : IHashRepository
{
    private int _remaining;
    private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Reset(int expectedMessageCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedMessageCount);

        _remaining = expectedMessageCount;
        _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task WaitAsync(CancellationToken ct = default)
    {
        ct.Register(() => _tcs.TrySetCanceled(ct));
        return _tcs.Task;
    }

    public Task SaveBatchAsync(IReadOnlyCollection<HashEntity> entities, CancellationToken ct = default)
    {
        if (Interlocked.Decrement(ref _remaining) <= 0)
            _tcs.TrySetResult();

        return Task.CompletedTask;
    }

    public Task<long> GetCountByDateAsync(DateOnly date, CancellationToken ct = default)
    {
        return Task.FromResult(0L);
    }
}
