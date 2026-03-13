using HashProcessing.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using RabbitMQ.Client;

namespace HashProcessing.Api.UnitTests.Infrastructure;

public class RabbitMqChannelPoolShould
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

        var pool = new ConsumerChannelPool(
            connection,
            NullLoggerFactory.Instance.CreateLogger<ConsumerChannelPool>());

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

        var pool = new ConsumerChannelPool(
            connection,
            NullLoggerFactory.Instance.CreateLogger<ConsumerChannelPool>());

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
        var logger = Substitute.For<ILogger<ConsumerChannelPool>>();

        // Act
        _ = new ConsumerChannelPool(connection, logger, maxSize: 1);

        // Assert
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
