using System.Threading.Channels;
using HashProcessing.Api.Core;
using static HashProcessing.Api.Infrastructure.Util;

namespace HashProcessing.Api.Infrastructure;

public class ParallelHashGenerator : IHashGenerator
{
    private readonly ushort _degreeOfParallelism;
    private readonly ushort _channelCapacity;

    public ParallelHashGenerator(ushort degreeOfParallelism, Func<ushort, ushort>? channelCapacitySelector)
    {
        _degreeOfParallelism = EnsureDegreeOfParallelism(degreeOfParallelism);

        var requestedCapacity =
            channelCapacitySelector?.Invoke(_degreeOfParallelism) ?? (ushort)(_degreeOfParallelism * 2);
        _channelCapacity = EnsureChannelCapacity(requestedCapacity);
    }

    public ChannelReader<Sha1Hash> StreamSha1s(
        uint count,
        CancellationToken ct = default)
    {
        var channel = Channel.CreateBounded<Sha1Hash>(
            new BoundedChannelOptions(_channelCapacity)
            {
                SingleWriter = false,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        if (ct.IsCancellationRequested)
        {
            channel.Writer.TryComplete(new OperationCanceledException(ct));
            return channel.Reader;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await Parallel.ForAsync(0u, count,
                    new ParallelOptions
                    {
                        MaxDegreeOfParallelism = _degreeOfParallelism,
                        CancellationToken = ct
                    },
                    async (_, localCt) =>
                    {
                        var sha1 = GenerateSha1();
                        await channel.Writer.WriteAsync(sha1, localCt);
                    });

                channel.Writer.TryComplete();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                channel.Writer.TryComplete(new OperationCanceledException(ct));
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        }, ct);

        return channel.Reader;
    }
}