using System.Threading.Channels;
using HashProcessing.Api.Core;
using static HashProcessing.Api.Infrastructure.Util;

namespace HashProcessing.Api.Infrastructure;

public class DefaultHashGenerator(ushort channelCapacity) : IHashGenerator
{
    private readonly ushort _channelCapacity = EnsureChannelCapacity(channelCapacity);

    public ChannelReader<Sha1Hash> StreamSha1s(uint count, CancellationToken cancellationToken = default)
    {
        var channel = Channel.CreateBounded<Sha1Hash>(new BoundedChannelOptions(_channelCapacity)
        {
            SingleWriter = true,
            SingleReader = false,
            FullMode = BoundedChannelFullMode.Wait
        });
        
        if (cancellationToken.IsCancellationRequested)
        {
            channel.Writer.TryComplete(new OperationCanceledException(cancellationToken));
            return channel.Reader;
        }
        
        _ = Task.Run(async () =>
        {
            try
            {
                for (var i = 0u; i < count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var sha1 = GenerateSha1();
                    await channel.Writer.WriteAsync(sha1, cancellationToken);
                }

                channel.Writer.TryComplete();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                channel.Writer.TryComplete(new OperationCanceledException(cancellationToken));
            }
        }, cancellationToken);
        
        return channel.Reader;
    }
}