using HashProcessing.Contracts;
using HashProcessing.Worker.Core;

namespace HashProcessing.Worker.Infrastructure;

public static class HashBatchMessageMapper
{
    public static IReadOnlyCollection<HashEntity> ToEntities(this HashBatchMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return [.. message.Hashes
            .Select(h => new HashEntity(
                h.Id,
                DateOnly.FromDateTime(h.Date.UtcDateTime),
                h.Value))];
    }
}
