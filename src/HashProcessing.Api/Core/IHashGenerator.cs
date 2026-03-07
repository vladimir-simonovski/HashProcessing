using System.Threading.Channels;

namespace HashProcessing.Api.Core;

public interface IHashGenerator
{
    // ReSharper disable once InconsistentNaming
    ChannelReader<Sha1Hash> StreamSha1s(uint count, CancellationToken cancellationToken = default);
}