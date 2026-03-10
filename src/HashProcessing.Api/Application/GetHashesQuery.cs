using HashProcessing.Api.Core;

namespace HashProcessing.Api.Application;

public record GetHashesQuery;

public record HashesResponse(IReadOnlyCollection<HashDateCount> Hashes);
public record HashDateCount(string Date, long Count);

public class GetHashesQueryHandler(IHashDailyCountRepository repository)
{
    private readonly IHashDailyCountRepository _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<HashesResponse> HandleAsync(CancellationToken ct = default)
    {
        var counts = await _repository.GetAllAsync(ct);

        var hashes = counts
            .Select(c => new HashDateCount(c.Date.ToString("yyyy-MM-dd"), c.Count))
            .ToList();

        return new HashesResponse(hashes);
    }
}
