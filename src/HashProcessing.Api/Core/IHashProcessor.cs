using System.Threading.Channels;

namespace HashProcessing.Api.Core;

public interface IHashProcessor
{
    Task<ProcessResult> ProcessAsync<THash>(
        ChannelReader<THash> hashChannelReader, 
        CancellationToken ct = default)
        where THash : IGeneratedHash;
}

public record struct ProcessResult(uint StreamedCount, uint ProcessedCount);
