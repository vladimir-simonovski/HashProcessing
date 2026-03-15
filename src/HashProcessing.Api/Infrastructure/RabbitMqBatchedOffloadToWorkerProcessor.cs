using System.Threading.Channels;
using HashProcessing.Api.Core;
using HashProcessing.Messaging;
using Microsoft.Extensions.Options;
using static HashProcessing.Api.Infrastructure.Util;

namespace HashProcessing.Api.Infrastructure;

public class RabbitMqBatchedOffloadToWorkerProcessor(
    RabbitMqPublisher publisher,
    IOptions<HashProcessingOptions> options)
    : IHashProcessor
{
    private readonly RabbitMqPublisher _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    private readonly IOptions<HashProcessingOptions> _options = options ?? throw new ArgumentNullException(nameof(options));

    public async Task<ProcessResult> ProcessAsync<THash>(
        ChannelReader<THash> hashChannelReader,
        CancellationToken ct = default)
        where THash : IGeneratedHash
    {
        var opts = _options.Value;
        ArgumentOutOfRangeException.ThrowIfZero(opts.BatchSize);

        var degreeOfParallelism = EnsureDegreeOfParallelism(opts.DegreeOfParallelism);
        var channelCapacity = EnsureChannelCapacity((ushort)(degreeOfParallelism * 2));

        var batchChannel = Channel.CreateBounded<IGeneratedHash[]>(
            new BoundedChannelOptions(channelCapacity)
            {
                SingleWriter = true,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait
            });

        var tcsBatchStreaming
            = StartBatchChannelStreaming(
                hashChannelReader,
                batchChannel.Writer,
                ct);

        var publishingTasks = new List<Task<uint>>(degreeOfParallelism);

        for (var i = 0; i < degreeOfParallelism; i++)
            publishingTasks.Add(PublishAsync(batchChannel.Reader, ct));

        var publishResults = await Task.WhenAll(publishingTasks);

        return new ProcessResult(
            await tcsBatchStreaming.Task,
            publishResults.Aggregate(0u, (acc, r) => acc + r));
    }

    private TaskCompletionSource<uint> StartBatchChannelStreaming<THash>(
        ChannelReader<THash> hashChannelReader,
        ChannelWriter<IGeneratedHash[]> batchChannelWriter,
        CancellationToken ct) where THash : IGeneratedHash
    {
        var batchSize = _options.Value.BatchSize;
        var tcsStreaming = new TaskCompletionSource<uint>();
        
        _ = Task.Run(async () =>
        {
            var streamedCount = 0u;

            try
            {
                var batch = new List<IGeneratedHash>(batchSize);

                while (await hashChannelReader.WaitToReadAsync(ct))
                {
                    while (batch.Count < batchSize && hashChannelReader.TryRead(out var hash))
                    {
                        batch.Add(hash);
                    }

                    if (batch.Count < batchSize) continue;

                    await batchChannelWriter.WriteAsync(batch.ToArray(), ct);
                    streamedCount += (uint)batch.Count;
                    batch.Clear();
                }

                if (batch.Count > 0)
                {
                    await batchChannelWriter.WriteAsync(batch.ToArray(), ct);
                    streamedCount += (uint)batch.Count;
                    batch.Clear();
                }

                batchChannelWriter.TryComplete();
            }
            catch (Exception e)
            {
                batchChannelWriter.TryComplete(e);
            }
            finally
            {
                tcsStreaming.TrySetResult(streamedCount);
            }
        }, ct);

        return tcsStreaming;
    }

    private async Task<uint> PublishAsync(
        ChannelReader<IGeneratedHash[]> batchChannelReader,
        CancellationToken ct)
    {
        var opts = _options.Value;
        var queueName = opts.PublishQueueName;
        var queueArguments = new QueueArguments { DeadLetterExchange = opts.DeadLetterExchange };

        var publishedCount = 0u;

        await foreach (var batch in batchChannelReader.ReadAllAsync(ct))
        {
            var hashBatchMessage = batch.ToHashBatchMessage();

            await _publisher.PublishAsync(hashBatchMessage, queueName, queueArguments, ct);

            Interlocked.Add(ref publishedCount, (uint)batch.Length);
        }

        return publishedCount;
    }
}