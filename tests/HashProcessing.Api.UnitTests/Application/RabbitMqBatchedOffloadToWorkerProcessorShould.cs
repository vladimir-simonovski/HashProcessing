using System.Threading.Channels;
using HashProcessing.Api.Core;
using HashProcessing.Api.Infrastructure;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RabbitMQ.Client;

namespace HashProcessing.Api.UnitTests.Application;

public class RabbitMqBatchedOffloadToWorkerProcessorShould
{
    [Fact]
    public async Task ProcessAsync_ReturnsCorrectCountResults()
    {
        // Arrange
        var rabbitMqChannel = Substitute.For<IChannel>();
        rabbitMqChannel.IsOpen.Returns(true);
        rabbitMqChannel.QueueDeclareAsync(
                Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<bool>(),
                Arg.Any<IDictionary<string, object?>>(), Arg.Any<bool>(), Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new QueueDeclareOk("test-queue", 0, 0));

        var connection = Substitute.For<IConnection>();
        connection.CreateChannelAsync(Arg.Any<CreateChannelOptions?>(), Arg.Any<CancellationToken>())
            .Returns(rabbitMqChannel);

        var connectionFactory = Substitute.For<IConnectionFactory>();
        connectionFactory.CreateConnectionAsync(Arg.Any<CancellationToken>())
            .Returns(connection);
        
        var loggerFactory = Substitute.For<ILoggerFactory>();

        var processor = new RabbitMqBatchedOffloadToWorkerProcessor(
            connectionFactory,
            loggerFactory,
            degreeOfParallelism: 2,
            batchSize: 100,
            queueName: "test-queue"
        );
        
        var hashes = Enumerable.Range(0, 1000)
            .Select(i =>
            {
                var hash = Substitute.For<IGeneratedHash>();
                hash.Value.Returns($"Hash{i}");
                return hash;
            })
            .ToList();

        var hashChannel = Channel.CreateUnbounded<IGeneratedHash>();
        foreach (var hash in hashes)
            await hashChannel.Writer.WriteAsync(hash);
        hashChannel.Writer.Complete();

        // Act
        var result = await processor.ProcessAsync(hashChannel.Reader);
        
        // Assert
        Assert.Equal((uint)hashes.Count, result.StreamedCount);
        Assert.Equal((uint)hashes.Count, result.ProcessedCount);
    }
}