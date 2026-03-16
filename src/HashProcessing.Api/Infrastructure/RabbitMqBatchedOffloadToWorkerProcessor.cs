using System.Threading.Channels;
using HashProcessing.Api.Core;
using HashProcessing.Messaging;
using Microsoft.Extensions.Options;
using static HashProcessing.Api.Infrastructure.Util;

namespace HashProcessing.Api.Infrastructure;

public class RabbitMqBatchedOffloadToWorkerProcessor : IHashProcessor
{
    private readonly RabbitMqPublisher _publisher;
    private readonly ushort _degreeOfParallelism;
    private readonly ushort _channelCapacity;
    private readonly ushort _batchSize;
    private readonly string _publishQueueName;
    private readonly string _deadLetterExchange;
    
    public RabbitMqBatchedOffloadToWorkerProcessor(RabbitMqPublisher publisher,
        IOptions<HashProcessingOptions> options)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfZero(options.Value.BatchSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.PublishQueueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Value.DeadLetterExchange);
        
        _degreeOfParallelism = EnsureDegreeOfParallelism(options.Value.DegreeOfParallelism);
        _channelCapacity = EnsureChannelCapacity(options.Value.ChannelCapacity);
        _batchSize = options.Value.BatchSize;
        _publishQueueName = options.Value.PublishQueueName;
        _deadLetterExchange = options.Value.DeadLetterExchange;
    }

    public async Task<ProcessResult> ProcessAsync<THash>(
        ChannelReader<THash> hashChannelReader,
        CancellationToken ct = default)
        where THash : IGeneratedHash
    {
        var batchChannel = Channel.CreateBounded<IGeneratedHash[]>(
            new BoundedChannelOptions(_channelCapacity)
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

        var publishingTasks = new List<Task<uint>>(_degreeOfParallelism);

        for (var i = 0; i < _degreeOfParallelism; i++)
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
        var tcsStreaming = new TaskCompletionSource<uint>();
        
        _ = Task.Run(async () =>
        {
            var streamedCount = 0u;

            try
            {
                var batch = new List<IGeneratedHash>(_batchSize);

                while (await hashChannelReader.WaitToReadAsync(ct))
                {
                    while (batch.Count < _batchSize && hashChannelReader.TryRead(out var hash))
                    {
                        batch.Add(hash);
                    }

                    if (batch.Count < _batchSize) continue;

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
        var queueArguments = new QueueArguments { DeadLetterExchange = _deadLetterExchange };

        var publishedCount = 0u;

        await foreach (var batch in batchChannelReader.ReadAllAsync(ct))
        {
            var hashBatchMessage = batch.ToHashBatchMessage();

            await _publisher.PublishAsync(hashBatchMessage, _publishQueueName, queueArguments, ct);

            Interlocked.Add(ref publishedCount, (uint)batch.Length);
        }

        return publishedCount;
    }
}