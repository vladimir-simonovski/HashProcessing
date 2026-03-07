using HashProcessing.Api.Core;
using HashProcessing.Contracts;

namespace HashProcessing.Api.Infrastructure;

public static class Mappers
{
    public static HashBatchMessage ToHashBatchMessage(this IEnumerable<IGeneratedHash> generatedHashes)
        => new([..generatedHashes.Select(h => new Hash(h.Id, h.Date, h.Value))]);
}