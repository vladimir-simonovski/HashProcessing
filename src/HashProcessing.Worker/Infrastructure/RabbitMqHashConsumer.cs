using HashProcessing.Messaging;
using HashProcessing.Worker.Application;
using RabbitMQ.Client;

namespace HashProcessing.Worker.Infrastructure;

public class RabbitMqHashConsumer(
    IConnectionFactory connectionFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<RabbitMqHashConsumer> logger,
    string queueName,
    ushort prefetchCount = 10)
    : RabbitMqConsumer<HashBatchMessage>(connectionFactory, logger, queueName, prefetchCount)
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
