using System.Threading.Channels;
using HashProcessing.Api.Core;
using HashProcessing.Api.Infrastructure;
using HashProcessing.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

        var publisherPool = new PublisherChannelPool(
            connection,
            NullLoggerFactory.Instance.CreateLogger<PublisherChannelPool>());

        var publisher = new RabbitMqPublisher(
            publisherPool,
            NullLoggerFactory.Instance.CreateLogger<RabbitMqPublisher>());

        var options = Substitute.For<IOptionsMonitor<HashProcessingOptions>>();
        options.CurrentValue.Returns(new HashProcessingOptions
        {
            DegreeOfParallelism = 2,
            BatchSize = 100,
            PublishQueueName = "test-queue",
            DeadLetterExchange = "dlx"
        });

        var processor = new RabbitMqBatchedOffloadToWorkerProcessor(
            publisher,
            options
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