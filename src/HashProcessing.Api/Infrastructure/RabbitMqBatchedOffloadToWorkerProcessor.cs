using System.Threading.Channels;
using HashProcessing.Api.Core;
using HashProcessing.Messaging;
using RabbitMQ.Client;
using static HashProcessing.Api.Infrastructure.Util;

namespace HashProcessing.Api.Infrastructure;

public class RabbitMqBatchedOffloadToWorkerProcessor : IHashProcessor
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ushort _degreeOfParallelism;
    private readonly ushort _channelCapacity;
    private readonly ushort _batchSize;
    private readonly string _queueName;
    private readonly IDictionary<string, object?>? _queueArguments;

    public RabbitMqBatchedOffloadToWorkerProcessor(
        IConnectionFactory connectionFactory,
        ILoggerFactory loggerFactory,
        ushort degreeOfParallelism,
        ushort batchSize,
        string queueName,
        IDictionary<string, object?>? queueArguments = null,
        Func<ushort, ushort>? channelCapacitySelector = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        
        _degreeOfParallelism = EnsureDegreeOfParallelism(degreeOfParallelism);

        var requestedCapacity =
            channelCapacitySelector?.Invoke(_degreeOfParallelism) ?? (ushort)(_degreeOfParallelism * 2);
        _channelCapacity = EnsureChannelCapacity(requestedCapacity);

        ArgumentOutOfRangeException.ThrowIfZero(batchSize);
        _batchSize = batchSize;

        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        _queueName = queueName;

        _queueArguments = queueArguments;
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

        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);
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
        await using var publisher = new RabbitMqPublisher(
            await _connectionFactory.CreateConnectionAsync(ct),
            _loggerFactory.CreateLogger<RabbitMqPublisher>(),
            _queueName,
            _queueArguments
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