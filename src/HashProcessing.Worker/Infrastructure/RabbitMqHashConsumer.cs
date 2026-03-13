using HashProcessing.Messaging;
using HashProcessing.Worker.Application;

namespace HashProcessing.Worker.Infrastructure;

public class RabbitMqHashConsumer(
    ConsumerChannelPool channelPool,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqHashConsumer> logger,
    string queueName,
    ushort prefetchCount = 10,
    QueueArguments? queueArguments = null)
    : RabbitMqConsumer<HashBatchMessage>(channelPool, logger, queueName, prefetchCount, queueArguments)
{
    protected override async Task HandleMessageAsync(
        HashBatchMessage message,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ProcessReceivedHashesCommandHandler>();

        var entities = message.ToEntities();
        var command = new ProcessReceivedHashesCommand(entities);

        await handler.HandleAsync(command, ct);
    }
}
