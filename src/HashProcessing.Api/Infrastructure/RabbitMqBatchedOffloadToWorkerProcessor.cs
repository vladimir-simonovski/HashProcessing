using System.Text.Json;
using System.Threading.Channels;
using HashProcessing.Api.Core;
using RabbitMQ.Client;
using static HashProcessing.Api.Infrastructure.Util;

namespace HashProcessing.Api.Infrastructure;

public class RabbitMqBatchedOffloadToWorkerProcessor : IHashProcessor
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ushort _degreeOfParallelism;
    private readonly ushort _channelCapacity;
    private readonly ushort _batchSize;
    private readonly string _queueName;

    public RabbitMqBatchedOffloadToWorkerProcessor(
        IConnectionFactory connectionFactory,
        ushort degreeOfParallelism,
        ushort batchSize,
        string queueName,
        Func<ushort, ushort>? channelCapacitySelector = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

        _degreeOfParallelism = EnsureDegreeOfParallelism(degreeOfParallelism);

        var requestedCapacity =
            channelCapacitySelector?.Invoke(_degreeOfParallelism) ?? (ushort)(_degreeOfParallelism * 2);
        _channelCapacity = EnsureChannelCapacity(requestedCapacity);

        ArgumentOutOfRangeException.ThrowIfZero(batchSize);
        _batchSize = batchSize;

        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        _queueName = queueName;
    }

    public async Task<ProcessResult> ProcessAsync<THash>(
        ChannelReader<THash> hashChannelReader,
        CancellationToken ct = default)
        where THash : IHash
    {
        var batchChannel = Channel.CreateBounded<IHash[]>(
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
        ChannelWriter<IHash[]> batchChannelWriter,
        CancellationToken ct) where THash : IHash
    {
        var tcsStreaming = new TaskCompletionSource<uint>();

        _ = Task.Run(async () =>
        {
            var streamedCount = 0u;

            try
            {
                var batch = new List<IHash>(_batchSize);

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
        ChannelReader<IHash[]> batchChannelReader,
        CancellationToken ct)
    {
        await using var connection = await _connectionFactory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.QueueDeclareAsync(
            _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json"
        };

        var publishedCount = 0u;

        await foreach (var batch in batchChannelReader.ReadAllAsync(ct))
        {
            var hashValues = batch.Select(h => h.Value).ToArray();
            var messageBody = JsonSerializer.SerializeToUtf8Bytes(hashValues);

            await channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: _queueName,
                mandatory: false,
                basicProperties: properties,
                body: messageBody,
                cancellationToken: ct);

            publishedCount += (uint)batch.Length;
        }

        return publishedCount;
    }
}