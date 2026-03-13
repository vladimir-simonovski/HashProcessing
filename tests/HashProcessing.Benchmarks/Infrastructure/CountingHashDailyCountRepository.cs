using HashProcessing.Api.Core;
using HashProcessing.Api.Infrastructure;

namespace HashProcessing.Benchmarks.Infrastructure;

public sealed class CompletionSignal
{
    private int _remaining;
    private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Reset(int expectedCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(expectedCount);

        _remaining = expectedCount;
        _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task WaitAsync(CancellationToken ct = default)
    {
        ct.Register(() => _tcs.TrySetCanceled(ct));
        return _tcs.Task;
    }

    public void Decrement()
    {
        if (Interlocked.Decrement(ref _remaining) <= 0)
            _tcs.TrySetResult();
    }
}

public sealed class CountingHashDailyCountRepository(
    HashDailyCountRepository inner,
    CompletionSignal signal) : IHashDailyCountRepository
{
    public async Task UpsertAsync(DateOnly date, long count, CancellationToken ct = default)
    {
        await inner.UpsertAsync(date, count, ct);
        signal.Decrement();
    }

    public Task<IReadOnlyCollection<HashDailyCount>> GetAllAsync(CancellationToken ct = default)
    {
        return inner.GetAllAsync(ct);
    }
}
