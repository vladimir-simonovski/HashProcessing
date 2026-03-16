using HashProcessing.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RabbitMQ.Client;

namespace HashProcessing.Api.UnitTests.Infrastructure;

public class PublisherChannelPoolShould
{
    [Fact]
    public async Task AcquireAsync_ReusesReturnedChannel()
    {
        // Arrange
        var channel = Substitute.For<IChannel>();
        channel.IsOpen.Returns(true);

        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(channel);

        var pool = new PublisherChannelPool(
            connection,
            NullLoggerFactory.Instance.CreateLogger<PublisherChannelPool>());

        // Act — acquire and release
        var lease1 = await pool.AcquireAsync();
        var channel1 = lease1.Channel;
        await lease1.DisposeAsync();

        // acquire again — should reuse
        var lease2 = await pool.AcquireAsync();
        var channel2 = lease2.Channel;
        await lease2.DisposeAsync();

        // Assert
        Assert.Same(channel1, channel2);
        await connection.Received(1)
            .CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AcquireAsync_WhenReturnedChannelIsDead_CreatesNew()
    {
        // Arrange
        var deadChannel = Substitute.For<IChannel>();
        deadChannel.IsOpen.Returns(true);

        var freshChannel = Substitute.For<IChannel>();
        freshChannel.IsOpen.Returns(true);

        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(deadChannel, freshChannel);

        var pool = new PublisherChannelPool(
            connection,
            NullLoggerFactory.Instance.CreateLogger<PublisherChannelPool>());

        // Act — acquire first, then mark dead, release, reacquire
        var lease1 = await pool.AcquireAsync();
        deadChannel.IsOpen.Returns(false);
        await lease1.DisposeAsync();

        var lease2 = await pool.AcquireAsync();

        // Assert
        Assert.Same(freshChannel, lease2.Channel);
        await lease2.DisposeAsync();
    }

    [Fact]
    public void Constructor_WhenMaxSizeLessThanProcessorCount_LogsWarning()
    {
        // Arrange
        var connection = Substitute.For<IConnection>();
        var logger = Substitute.For<ILogger<PublisherChannelPool>>();

        // Act
        _ = new PublisherChannelPool(connection, logger, maxSize: 1);

        // Assert
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task AcquireAsync_ConcurrentPublishers_ReuseChannelsWithoutExcessiveCreation()
    {
        // Arrange — pool of 4, 8 concurrent publishers each doing 10 publishes
        const int poolSize = 4;
        const int concurrency = 8;
        const int publishesPerThread = 10;

        var channels = Enumerable.Range(0, poolSize)
            .Select(_ =>
            {
                var ch = Substitute.For<IChannel>();
                ch.IsOpen.Returns(true);
                return ch;
            })
            .ToArray();

        var callIndex = 0;
        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                var idx = Interlocked.Increment(ref callIndex) - 1;
                return idx < channels.Length
                    ? channels[idx]
                    : throw new InvalidOperationException(
                        $"Created {idx + 1} channels but pool should cap at {poolSize}");
            });

        var pool = new PublisherChannelPool(
            connection,
            NullLoggerFactory.Instance.CreateLogger<PublisherChannelPool>(),
            maxSize: poolSize);

        // Act — 8 threads each acquire/release 10 times
        var tasks = Enumerable.Range(0, concurrency).Select(_ => Task.Run(async () =>
        {
            for (var i = 0; i < publishesPerThread; i++)
            {
                await using var lease = await pool.AcquireAsync();
                // simulate publish work
                await Task.Yield();
            }
        }));

        await Task.WhenAll(tasks);

        // Assert — at most poolSize channels created, all reused
        await connection.Received(poolSize)
            .CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Return_WhenChannelClosedDuringUse_DoesNotCauseExcessiveRecreation()
    {
        // Arrange — simulates daily-count publisher scenario:
        // channel closes mid-use, pool should create ONE replacement, not recreate every time
        const int poolSize = 2;
        const int totalPublishes = 20;

        var liveChannel = Substitute.For<IChannel>();
        liveChannel.IsOpen.Returns(true);

        var dyingChannel = Substitute.For<IChannel>();
        dyingChannel.IsOpen.Returns(true);

        var replacementChannel = Substitute.For<IChannel>();
        replacementChannel.IsOpen.Returns(true);

        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(liveChannel, dyingChannel, replacementChannel);

        var pool = new PublisherChannelPool(
            connection,
            NullLoggerFactory.Instance.CreateLogger<PublisherChannelPool>(),
            maxSize: poolSize);

        // Act — acquire both channels
        var lease1 = await pool.AcquireAsync();
        var lease2 = await pool.AcquireAsync();
        Assert.Same(liveChannel, lease1.Channel);
        Assert.Same(dyingChannel, lease2.Channel);

        // Simulate channel dying before return
        dyingChannel.IsOpen.Returns(false);
        await lease1.DisposeAsync();
        await lease2.DisposeAsync(); // closed channel — dropped from pool

        // Continue publishing with 2 concurrent acquires to force replacement creation
        var leaseA = await pool.AcquireAsync(); // gets liveChannel from queue
        var leaseB = await pool.AcquireAsync(); // queue empty → creates replacementChannel
        Assert.Same(liveChannel, leaseA.Channel);
        Assert.Same(replacementChannel, leaseB.Channel);
        await leaseA.DisposeAsync();
        await leaseB.DisposeAsync();

        // Now both healthy channels are in the pool — sequential publishes should reuse them
        for (var i = 0; i < totalPublishes; i++)
        {
            await using var lease = await pool.AcquireAsync();
            Assert.True(
                ReferenceEquals(lease.Channel, liveChannel) ||
                ReferenceEquals(lease.Channel, replacementChannel),
                "Unexpected channel instance — pool is recreating channels excessively");
        }

        // Assert — exactly 3 channels created: 2 initial + 1 replacement for the dead one
        await connection.Received(3)
            .CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>());
    }
}
