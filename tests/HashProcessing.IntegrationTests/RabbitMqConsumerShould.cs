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
        var consumer = new FailingConsumer(
            fixture.Connection,
            NullLogger.Instance,
            QueueName,
            queueArguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = "dlx" });

        using var cts = new CancellationTokenSource();
        var consumerTask = Task.Run(() => consumer.ConsumeAsync(1, cts.Token), cts.Token);

        await Task.Delay(500);

        var message = new TestMessage("hello");

        await using var publishChannel = await fixture.Connection.CreateChannelAsync();
        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var props = new BasicProperties { Persistent = true, ContentType = "application/json" };

        // Act
        await publishChannel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: QueueName,
            mandatory: false,
            basicProperties: props,
            body: body);

        // Assert — poll the DLQ
        await using var dlqChannel = await fixture.Connection.CreateChannelAsync();
        BasicGetResult? dlqMessage = null;
        var sw = Stopwatch.StartNew();

        while (sw.Elapsed < TimeSpan.FromSeconds(10))
        {
            dlqMessage = await dlqChannel.BasicGetAsync(DlqName, autoAck: true);
            if (dlqMessage is not null)
                break;
            await Task.Delay(200);
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
        IConnection connection,
        ILogger logger,
        string queueName,
        ushort prefetchCount = 1,
        IDictionary<string, object?>? queueArguments = null)
        : RabbitMqConsumer<TestMessage>(connection, logger, queueName, prefetchCount, queueArguments)
    {
        protected override Task HandleMessageAsync(TestMessage message, CancellationToken ct)
            => throw new InvalidOperationException("Simulated handler failure");
    }
}
