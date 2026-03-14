using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace HashProcessing.Messaging;

public class RabbitMqChannelPool : IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly CreateChannelOptions? _channelOptions;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentQueue<IChannel> _available = new();

    protected RabbitMqChannelPool(
        IConnection connection,
        ILogger logger,
        CreateChannelOptions? channelOptions = null,
        int maxSize = 0)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        var logger1 = logger ?? throw new ArgumentNullException(nameof(logger));
        _channelOptions = channelOptions;

        if (maxSize <= 0)
            maxSize = Environment.ProcessorCount * 2;

        if (maxSize < Environment.ProcessorCount)
            logger1.LogWarning(
                "Channel pool size {MaxSize} is less than processor count {ProcessorCount}",
                maxSize, Environment.ProcessorCount);

        _semaphore = new SemaphoreSlim(maxSize, maxSize);
    }

    public async Task<ChannelLease> AcquireAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);

        try
        {
            while (_available.TryDequeue(out var channel))
            {
                if (channel.IsOpen)
                    return new ChannelLease(channel, Return);

                try { await channel.DisposeAsync(); }
                catch { /* dead channel */ }
            }

            var newChannel = await _connection.CreateChannelAsync(_channelOptions, ct);
            return new ChannelLease(newChannel, Return);
        }
        catch
        {
            _semaphore.Release();
            throw;
        }
    }

    private void Return(IChannel channel)
    {
        if (channel.IsOpen)
            _available.Enqueue(channel);

        _semaphore.Release();
    }

    public async ValueTask DisposeAsync()
    {
        while (_available.TryDequeue(out var channel))
        {
            try { await channel.DisposeAsync(); }
            catch { /* best-effort cleanup */ }
        }

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}

public sealed class PublisherChannelPool(IConnection connection, ILogger<PublisherChannelPool> logger, int maxSize = 0)
    : RabbitMqChannelPool(connection, logger, PublisherOptions, maxSize)
{
    private static readonly CreateChannelOptions PublisherOptions = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);
}

public sealed class ConsumerChannelPool(IConnection connection, ILogger<ConsumerChannelPool> logger, int maxSize = 0)
    : RabbitMqChannelPool(connection, logger, channelOptions: null, maxSize);