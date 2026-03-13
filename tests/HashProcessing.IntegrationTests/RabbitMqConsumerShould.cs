using System.Diagnostics;
using System.Text.Json;
using HashProcessing.IntegrationTests.Fixtures;
using HashProcessing.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace HashProcessing.IntegrationTests;

public class RabbitMqConsumerShould(RabbitMqFixture fixture) : IClassFixture<RabbitMqFixture>
{
    private const string QueueName = "test-queue";
    private const string DlqName = "test-dlq";

    [Fact]
    public async Task ConsumeAsync_WhenHandlerThrows_RoutesMessageToDeadLetterQueue()
    {
        // Arrange
        var consumerPool = new ConsumerChannelPool(
            fixture.Connection,
            NullLoggerFactory.Instance.CreateLogger<ConsumerChannelPool>());

        var consumer = new FailingConsumer(
            consumerPool,
            NullLogger.Instance,
            QueueName,
            queueArguments: new QueueArguments { DeadLetterExchange = "dlx" });

        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        var consumerTask = Task.Run(() => consumer.ConsumeAsync(1, token), token);

        await Task.Delay(500, cts.Token);

        var message = new TestMessage("hello");

        await using var publishChannel = await fixture.Connection.CreateChannelAsync(cancellationToken: cts.Token);
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var props = new BasicProperties { Persistent = true, ContentType = "application/json" };

        // Act
        await publishChannel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: QueueName,
            mandatory: false,
            basicProperties: props,
            body: body, cancellationToken: cts.Token);

        // Assert — poll the DLQ
        await using var dlqChannel = await fixture.Connection.CreateChannelAsync(cancellationToken: cts.Token);
        BasicGetResult? dlqMessage = null;
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < TimeSpan.FromSeconds(10))
        {
            dlqMessage = await dlqChannel.BasicGetAsync(DlqName, autoAck: true, cancellationToken: cts.Token);
            if (dlqMessage is not null)
                break;
            await Task.Delay(200, cts.Token);
        }

        Assert.NotNull(dlqMessage);

        var deadLettered = JsonSerializer.Deserialize<TestMessage>(dlqMessage.Body.Span);
        Assert.NotNull(deadLettered);
        Assert.Equal(message.Payload, deadLettered.Payload);

        await cts.CancelAsync();
        try { await consumerTask; }
        catch (OperationCanceledException) { }
    }

    private record TestMessage(string Payload) : MessageBase;

    private sealed class FailingConsumer(
        ConsumerChannelPool channelPool,
        ILogger logger,
        string queueName,
        ushort prefetchCount = 1,
        QueueArguments? queueArguments = null)
        : RabbitMqConsumer<TestMessage>(channelPool, logger, queueName, prefetchCount, queueArguments)
    {
        protected override Task HandleMessageAsync(TestMessage message, CancellationToken ct)
            => throw new InvalidOperationException("Simulated handler failure");
    }
}
