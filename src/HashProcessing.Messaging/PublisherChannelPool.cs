using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace HashProcessing.Messaging;

public sealed class PublisherChannelPool : IAsyncDisposable
{
    private static readonly CreateChannelOptions ChannelOptions = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true);

    private readonly IConnection _connection;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentQueue<IChannel> _available = new();

    public PublisherChannelPool(
        IConnection connection,
        ILogger<PublisherChannelPool> logger,
        int maxSize = 0)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        ArgumentNullException.ThrowIfNull(logger);

        if (maxSize <= 0)
            maxSize = Environment.ProcessorCount * 2;

        if (maxSize < Environment.ProcessorCount)
            logger.LogWarning(
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

            var newChannel = await _connection.CreateChannelAsync(ChannelOptions, ct);
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
    }
}