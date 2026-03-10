namespace HashProcessing.Api.Core;

public interface IHashDailyCountRepository
{
    Task UpsertAsync(DateOnly date, long count, CancellationToken ct = default);
    Task<IReadOnlyCollection<HashDailyCount>> GetAllAsync(CancellationToken ct = default);
}
