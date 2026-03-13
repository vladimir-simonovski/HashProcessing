using System.Threading.Channels;
using HashProcessing.Api.Core;
using HashProcessing.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using static HashProcessing.Api.Infrastructure.Util;

namespace HashProcessing.Api.Infrastructure;

public class RabbitMqBatchedOffloadToWorkerProcessor : IHashProcessor
{
    private readonly IConnection _connection;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptionsMonitor<HashProcessingOptions> _options;

    public RabbitMqBatchedOffloadToWorkerProcessor(
        IConnection connection,
        ILoggerFactory loggerFactory,
        IOptionsMonitor<HashProcessingOptions> options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<ProcessResult> ProcessAsync<THash>(
        ChannelReader<THash> hashChannelReader,
        CancellationToken ct = default)
        where THash : IGeneratedHash
    {
        var opts = _options.CurrentValue;
        ArgumentOutOfRangeException.ThrowIfZero(opts.BatchSize);
        ArgumentException.ThrowIfNullOrWhiteSpace(opts.PublishQueueName);

        var degreeOfParallelism = EnsureDegreeOfParallelism(opts.DegreeOfParallelism);
        var channelCapacity = EnsureChannelCapacity((ushort)(degreeOfParallelism * 2));
        var queueArguments = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = opts.DeadLetterExchange };

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
                opts.BatchSize,
                ct);

        var publishingTasks = new List<Task<uint>>(degreeOfParallelism);

        for (var i = 0; i < degreeOfParallelism; i++)
            publishingTasks.Add(PublishAsync(batchChannel.Reader, opts.PublishQueueName, queueArguments, ct));

        var publishResults = await Task.WhenAll(publishingTasks);

        return new ProcessResult(
            await tcsBatchStreaming.Task,
            publishResults.Aggregate(0u, (acc, r) => acc + r));
    }

    private TaskCompletionSource<uint> StartBatchChannelStreaming<THash>(
        ChannelReader<THash> hashChannelReader,
        ChannelWriter<IGeneratedHash[]> batchChannelWriter,
        ushort batchSize,
        CancellationToken ct) where THash : IGeneratedHash
    {
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
        string queueName,
        IDictionary<string, object?>? queueArguments,
        CancellationToken ct)
    {
        await using var publisher = new RabbitMqPublisher(
            _connection,
            _loggerFactory.CreateLogger<RabbitMqPublisher>(),
            queueName,
            queueArguments
        );

        var publishedCount = 0u;

        await foreach (var batch in batchChannelReader.ReadAllAsync(ct))
        {
            var hashBatchMessage = batch.ToHashBatchMessage();

            await publisher.PublishAsync(hashBatchMessage, ct);

            Interlocked.Add(ref publishedCount, (uint)batch.Length);
        }

        return publishedCount;
    }
}